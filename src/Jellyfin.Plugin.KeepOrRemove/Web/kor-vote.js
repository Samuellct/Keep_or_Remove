/*
 * Keep or Remove - detail-page vote buttons.
 *
 * Design constraints (see Synthese.md sections 14, 16, 17):
 *  - Defensive and silent: if an expected element is missing, skip. Never throw into Jellyfin Web.
 *  - On demand only: one API read per target, cached for the session. No polling, no broad
 *    MutationObserver loops - a single debounced page-change hook.
 *  - Other users' votes are never shown here.
 *
 * Pure helpers are exported for vitest; the DOM wiring runs only in a browser.
 */

(function () {
    'use strict';

    var SUPPORTED_TYPES = ['Movie', 'Series', 'Season', 'Episode'];
    var voteCache = new Map(); // targetItemId -> 'KEEP' | 'REMOVE' | null

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

    var api = {
        _isDetailPage: _isDetailPage,
        _detailItemId: _detailItemId,
        _isSupportedType: _isSupportedType,
        _voteTargetId: _voteTargetId,
        _escHtml: _escHtml
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }

    if (typeof document === 'undefined' || typeof window === 'undefined') {
        return;
    }

    // TODO (PLAN.md Phase 4): debounced viewshow/hashchange hook -> resolve current item via
    // ApiClient -> GET /KeepOrRemove/vote (cached in voteCache) -> render two buttons into the
    // detail action row using an existing anchor, skipping silently if absent -> PUT on click.
    void voteCache;
    console.info('[KeepOrRemove] loaded (buttons wiring not yet implemented - PLAN.md Phase 4).');
})();
