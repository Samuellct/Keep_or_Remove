/*
 * Keep or Remove - detail-page vote buttons: render the stored vote, and cast / change / clear it.
 *
 * Design constraints (Synthese.md sections 2, 13, 14, 16, 17):
 *  - Defensive and silent: a missing anchor -> skip. Never throw into Jellyfin Web.
 *  - On demand only: one /vote read per target, cached for the session. No polling; a single
 *    guarded MutationObserver plus a hashchange re-arm.
 *  - Clicking is optimistic: the visual state flips immediately and rolls back if the write fails
 *    (console.warn only, nothing on screen). Diverges from Homepage's pessimistic toggles on
 *    purpose - the rollback path is exercised by the phase-4 manual test.
 *  - Other users' votes are never shown here.
 *
 * Pure helpers are exported for Vitest; the browser section (after the export) never runs under
 * jsdom because `module` is defined there and the IIFE returns immediately.
 */

(function () {
    'use strict';

    var SUPPORTED_TYPES = ['Movie', 'Series', 'Season', 'Episode'];

    // Anchored: only the item-detail route, not a library listing that happens to contain "details".
    function _isDetailPage(hash) {
        return /^#\/details([/?]|$)/i.test(hash || '');
    }

    // Jellyfin item ids are 32-hex (no dashes) in the URL; the older 36-char dashed form is also
    // accepted. "&serverId=" does not false-match because [#&?] must sit right before "id=".
    function _detailItemId(hash) {
        if (typeof hash !== 'string') {
            return null;
        }
        var m = /[#&?]id=([a-f0-9-]+)/i.exec(hash);
        return m ? m[1] : null;
    }

    function _isSupportedType(item) {
        return !!item && SUPPORTED_TYPES.indexOf(item.Type) !== -1;
    }

    // The id the vote belongs to: the parent series for a season/episode (getItem returns SeriesId
    // on those DTOs), else the item itself. Falls back to the item's own id so a supported item
    // never yields null - the backend resolves either way (VoteService.ResolveVoteTargetId).
    function _voteTargetId(item) {
        if (!item) {
            return null;
        }
        if (item.Type === 'Season' || item.Type === 'Episode') {
            return item.SeriesId || item.Id || null;
        }
        return item.Id || null;
    }

    function _escHtml(value) {
        if (value === null || value === undefined) {
            return '';
        }
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    // Which of the two buttons carries kor-active for a stored vote value.
    function _activeKind(vote) {
        if (vote === 'KEEP') {
            return 'keep';
        }
        if (vote === 'REMOVE') {
            return 'remove';
        }
        return null;
    }

    // Three-state toggle: clicking the inactive button switches to it; clicking the active one
    // clears the vote. `current` is 'KEEP' | 'REMOVE' | null, `clicked` is 'KEEP' | 'REMOVE'.
    function _nextVoteState(current, clicked) {
        return current === clicked ? null : clicked;
    }

    // Transport-agnostic descriptor for the write the caller must perform. A non-null vote is an
    // upsert (PUT with a body); null is a removal (DELETE with a query param). Keeps the PUT/DELETE
    // branch decision unit-testable without a real ApiClient.
    function _voteRequest(targetId, next) {
        if (next === null) {
            return { method: 'DELETE', params: { itemId: targetId } };
        }
        return { method: 'PUT', body: { itemId: targetId, vote: next } };
    }

    // Reflects a vote value onto an existing container's two buttons: kor-active + aria-pressed on
    // the matching one, cleared on the other. Used both for the initial render and for optimistic
    // updates / rollbacks.
    function _applyVoteState(container, vote) {
        var active = _activeKind(vote);
        var buttons = container.querySelectorAll('.kor-vote-button');
        Array.prototype.forEach.call(buttons, function (button) {
            var isActive = button.classList.contains('kor-vote-' + active);
            button.classList.toggle('kor-active', isActive);
            button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        });
    }

    // Detached DOM: a <span class="kor-vote"> holding two <button> children styled like native
    // .detailButton entries. The button matching the stored vote (if any) gets kor-active /
    // aria-pressed via _applyVoteState. Click handling is bound later by _bindVoteClicks.
    function _buildVoteContainer(vote) {
        var container = document.createElement('span');
        container.className = 'kor-vote';

        var specs = [
            { kind: 'keep', icon: 'thumb_up', label: 'Keep' },
            { kind: 'remove', icon: 'thumb_down', label: 'Remove' }
        ];

        specs.forEach(function (spec) {
            var button = document.createElement('button');
            button.type = 'button';
            // Cosmetic only: the document-register-element polyfill does not upgrade an `is` set
            // after createElement, so `emby-button` is added to the class list explicitly to match
            // the native detail buttons visually. Click handling is bound directly by _bindVoteClicks.
            button.setAttribute('is', 'emby-button');
            button.className = 'button-flat detailButton emby-button kor-vote-button kor-vote-' + spec.kind;
            button.title = spec.label;
            button.setAttribute('aria-pressed', 'false');

            var content = document.createElement('div');
            content.className = 'detailButton-content';

            var iconEl = document.createElement('span');
            iconEl.className = 'material-icons detailButton-icon ' + spec.icon;
            iconEl.setAttribute('aria-hidden', 'true');

            content.appendChild(iconEl);
            button.appendChild(content);
            container.appendChild(button);
        });

        _applyVoteState(container, vote);
        return container;
    }

    var api = {
        _isDetailPage: _isDetailPage,
        _detailItemId: _detailItemId,
        _isSupportedType: _isSupportedType,
        _voteTargetId: _voteTargetId,
        _escHtml: _escHtml,
        _activeKind: _activeKind,
        _nextVoteState: _nextVoteState,
        _voteRequest: _voteRequest,
        _applyVoteState: _applyVoteState,
        _buildVoteContainer: _buildVoteContainer
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
        return;
    }

    if (typeof window === 'undefined' || typeof document === 'undefined') {
        return;
    }

    // ---------------------------------------------------------------------------
    // Browser section - runs only in a real Jellyfin Web page.
    // ---------------------------------------------------------------------------

    var voteCache = new Map(); // targetId -> 'KEEP' | 'REMOVE' | null
    var observer = null;

    function _retry(probe, attemptsLeft, delayMs) {
        var value = probe();
        if (value) {
            return Promise.resolve(value);
        }
        if (attemptsLeft <= 0) {
            return Promise.resolve(null);
        }
        return new Promise(function (resolve) {
            setTimeout(function () {
                resolve(_retry(probe, attemptsLeft - 1, delayMs));
            }, delayMs);
        });
    }

    function _waitForApiClient() {
        return _retry(function () { return window.ApiClient; }, 10, 300);
    }

    function _waitForUserId() {
        return _retry(function () {
            return window.ApiClient && window.ApiClient.getCurrentUserId();
        }, 25, 300);
    }

    function _fetchVote(targetId) {
        if (voteCache.has(targetId)) {
            return Promise.resolve(voteCache.get(targetId));
        }
        var url = window.ApiClient.getUrl('KeepOrRemove/vote', { itemId: targetId });
        return window.ApiClient.getJSON(url).then(function (resp) {
            var vote = resp && resp.vote ? resp.vote : null;
            voteCache.set(targetId, vote);
            return vote;
        }).catch(function (err) {
            console.warn('[KeepOrRemove] could not read the current vote:', err);
            // Cache the miss so repeated MutationObserver ticks do not re-request within the session.
            voteCache.set(targetId, null);
            return null;
        });
    }

    // Performs the write described by _voteRequest. Resolves on 2xx, rejects otherwise - the caller
    // rolls back on rejection.
    function _sendVote(targetId, next) {
        var desc = _voteRequest(targetId, next);
        if (desc.method === 'DELETE') {
            return window.ApiClient.ajax({
                type: 'DELETE',
                url: window.ApiClient.getUrl('KeepOrRemove/vote', desc.params)
            });
        }
        return window.ApiClient.ajax({
            type: 'PUT',
            url: window.ApiClient.getUrl('KeepOrRemove/vote'),
            data: JSON.stringify(desc.body),
            contentType: 'application/json'
        });
    }

    function _pulse(button) {
        button.classList.add('kor-saved');
        setTimeout(function () { button.classList.remove('kor-saved'); }, 400);
    }

    function _onVoteClick(container, button, targetId, clicked) {
        if (container.dataset.korBusy === '1') {
            return; // a write is already in flight for this container
        }

        var previous = voteCache.has(targetId) ? voteCache.get(targetId) : null;
        var next = _nextVoteState(previous, clicked);

        // Optimistic: reflect the new state before the network call, guard against re-entry.
        voteCache.set(targetId, next);
        _applyVoteState(container, next);
        container.dataset.korBusy = '1';
        container.classList.add('kor-busy');

        _sendVote(targetId, next).then(function () {
            _pulse(button);
        }).catch(function (err) {
            voteCache.set(targetId, previous);
            if (container.isConnected) {
                _applyVoteState(container, previous);
            }
            console.warn('[KeepOrRemove] could not save the vote:', err);
        }).then(function () {
            delete container.dataset.korBusy;
            container.classList.remove('kor-busy');
        });
    }

    // Binds one click listener per button. targetId is fixed for the life of the container (the
    // detail row is rebuilt on navigation), so it is captured here rather than re-read on click.
    function _bindVoteClicks(container, targetId) {
        var buttons = container.querySelectorAll('.kor-vote-button');
        Array.prototype.forEach.call(buttons, function (button) {
            var clicked = button.classList.contains('kor-vote-keep') ? 'KEEP' : 'REMOVE';
            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                _onVoteClick(container, button, targetId, clicked);
            });
        });
    }

    function _activePage() {
        return document.querySelector('#itemDetailPage:not(.hide)')
            || document.querySelector('.libraryPage:not(.hide)');
    }

    function _resolveAndRender(row, itemId) {
        return _waitForUserId().then(function (userId) {
            if (!userId) {
                return null;
            }
            return window.ApiClient.getItem(userId, itemId);
        }).then(function (item) {
            if (row.dataset.korPending !== itemId) {
                return null; // the hash changed while getItem was in flight
            }
            if (!_isSupportedType(item)) {
                return null;
            }
            var targetId = _voteTargetId(item);
            if (!targetId) {
                return null;
            }
            return _fetchVote(targetId).then(function (vote) {
                if (row.dataset.korPending !== itemId || !row.isConnected) {
                    return null;
                }
                var stale = row.querySelector('.kor-vote');
                if (stale) {
                    stale.remove();
                }
                var container = _buildVoteContainer(vote);
                container.dataset.korItemId = itemId;
                container.dataset.korTargetId = targetId;
                _bindVoteClicks(container, targetId);
                var more = row.querySelector('.btnMoreCommands');
                if (more) {
                    row.insertBefore(container, more);
                } else {
                    row.appendChild(container);
                }
                return container;
            });
        });
    }

    function _tick() {
        if (typeof location === 'undefined' || typeof document === 'undefined') {
            return;
        }
        if (!_isDetailPage(location.hash)) {
            return;
        }

        var page = _activePage();
        if (!page) {
            return;
        }
        var row = page.querySelector('.mainDetailButtons');
        if (!row) {
            return;
        }

        var heart = row.querySelector('.btnUserRating');
        var itemId = (heart && heart.getAttribute('data-id')) || _detailItemId(location.hash);
        if (!itemId) {
            return; // setItem() has not populated the id yet; a later tick retries
        }

        var existing = row.querySelector('.kor-vote');
        if (existing) {
            if (existing.dataset.korItemId === itemId) {
                return; // already rendered for this item
            }
            existing.remove(); // the row was reused for a different item by an SPA navigation
        }
        if (row.dataset.korPending === itemId) {
            return;
        }
        row.dataset.korPending = itemId;

        _resolveAndRender(row, itemId)
            .catch(function (err) {
                console.error('[KeepOrRemove] failed to render the vote buttons:', err);
            })
            .then(function () {
                if (row.dataset.korPending === itemId) {
                    delete row.dataset.korPending;
                }
            });
    }

    function _startObserving() {
        observer = new MutationObserver(function () { _tick(); });
        observer.observe(document.body, { childList: true, subtree: true });
        window.addEventListener('hashchange', function () { setTimeout(_tick, 400); });
        _tick();
    }

    function _bootstrap() {
        _waitForApiClient().then(function (apiClient) {
            if (!apiClient) {
                return;
            }
            return apiClient.getJSON(apiClient.getUrl('KeepOrRemove/meta')).then(function (meta) {
                if (meta && meta.enabled === false) {
                    return; // the admin disabled the buttons; stay dormant, no observer
                }
                _startObserving();
            }).catch(function () {
                _startObserving(); // fail-open: a meta lookup failure must not hide the feature
            });
        }).catch(function (err) {
            console.error('[KeepOrRemove] bootstrap failed:', err);
        });
    }

    _bootstrap();
})();
