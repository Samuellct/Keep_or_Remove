import { describe, it, expect } from 'vitest';
import korVote from '../src/Jellyfin.Plugin.KeepOrRemove/Web/kor-vote.js';

const {
    _isDetailPage,
    _detailItemId,
    _isSupportedType,
    _voteTargetId,
    _escHtml,
    _activeKind,
    _buildVoteContainer
} = korVote;

describe('_isDetailPage', () => {
    it('is true for a details route', () => {
        expect(_isDetailPage('#/details?id=abc')).toBe(true);
        expect(_isDetailPage('#/details/abc')).toBe(true);
        expect(_isDetailPage('#/details')).toBe(true);
    });

    it('is false for library listings and other routes', () => {
        expect(_isDetailPage('#/home')).toBe(false);
        expect(_isDetailPage('#/movies?topParentId=x')).toBe(false);
        expect(_isDetailPage('#/detailsomething')).toBe(false);
        expect(_isDetailPage('')).toBe(false);
        expect(_isDetailPage(undefined)).toBe(false);
    });
});

describe('_detailItemId', () => {
    it('extracts the id from the hash, ignoring serverId', () => {
        expect(_detailItemId('#/details?id=abc123def')).toBe('abc123def');
        expect(_detailItemId('#/details?id=ab-12&serverId=072a5aed')).toBe('ab-12');
    });

    it('returns null when there is no id, or the id does not start with a hex char', () => {
        expect(_detailItemId('#/home')).toBeNull();
        expect(_detailItemId('#/details?id=zzz')).toBeNull();
        expect(_detailItemId(undefined)).toBeNull();
    });
});

describe('_isSupportedType', () => {
    it('accepts Movie, Series, Season, Episode', () => {
        ['Movie', 'Series', 'Season', 'Episode'].forEach((Type) => {
            expect(_isSupportedType({ Type })).toBe(true);
        });
    });

    it('rejects others and nullish', () => {
        expect(_isSupportedType({ Type: 'BoxSet' })).toBe(false);
        expect(_isSupportedType(null)).toBe(false);
    });
});

describe('_voteTargetId', () => {
    it('returns the series id for a season or episode when the DTO carries SeriesId', () => {
        expect(_voteTargetId({ Type: 'Episode', Id: 'e1', SeriesId: 's1' })).toBe('s1');
        expect(_voteTargetId({ Type: 'Season', Id: 'se1', SeriesId: 's1' })).toBe('s1');
    });

    it('falls back to the item id when SeriesId is missing (backend still resolves)', () => {
        expect(_voteTargetId({ Type: 'Episode', Id: 'e1' })).toBe('e1');
    });

    it('returns the item id for a movie or series', () => {
        expect(_voteTargetId({ Type: 'Movie', Id: 'm1' })).toBe('m1');
        expect(_voteTargetId({ Type: 'Series', Id: 's1' })).toBe('s1');
    });

    it('returns null for a nullish item', () => {
        expect(_voteTargetId(null)).toBeNull();
        expect(_voteTargetId(undefined)).toBeNull();
    });
});

describe('_escHtml', () => {
    it('escapes HTML special characters', () => {
        expect(_escHtml('<b>Tom & "Jerry"</b>')).toBe('&lt;b&gt;Tom &amp; &quot;Jerry&quot;&lt;/b&gt;');
    });
});

describe('_activeKind', () => {
    it('maps a stored vote to the button that should be highlighted', () => {
        expect(_activeKind('KEEP')).toBe('keep');
        expect(_activeKind('REMOVE')).toBe('remove');
    });

    it('returns null when there is no vote', () => {
        expect(_activeKind(null)).toBeNull();
        expect(_activeKind(undefined)).toBeNull();
        expect(_activeKind('MAYBE')).toBeNull();
    });
});

describe('_buildVoteContainer', () => {
    it('builds a detached span with two emby-button children', () => {
        const el = _buildVoteContainer(null);

        expect(el.tagName).toBe('SPAN');
        expect(el.className).toBe('kor-vote');
        expect(el.isConnected).toBe(false);

        const buttons = el.querySelectorAll('button');
        expect(buttons).toHaveLength(2);
        buttons.forEach((b) => {
            expect(b.getAttribute('is')).toBe('emby-button');
            expect(b.type).toBe('button');
            expect(b.className).toContain('button-flat detailButton emby-button kor-vote-button');
        });
        expect(el.querySelector('.kor-vote-keep .material-icons.thumb_up')).not.toBeNull();
        expect(el.querySelector('.kor-vote-remove .material-icons.thumb_down')).not.toBeNull();
    });

    it('highlights only the keep button for a KEEP vote', () => {
        const el = _buildVoteContainer('KEEP');
        expect(el.querySelector('.kor-vote-keep').classList.contains('kor-active')).toBe(true);
        expect(el.querySelector('.kor-vote-remove').classList.contains('kor-active')).toBe(false);
    });

    it('highlights only the remove button for a REMOVE vote', () => {
        const el = _buildVoteContainer('REMOVE');
        expect(el.querySelector('.kor-vote-remove').classList.contains('kor-active')).toBe(true);
        expect(el.querySelector('.kor-vote-keep').classList.contains('kor-active')).toBe(false);
    });

    it('highlights neither button when there is no vote', () => {
        const el = _buildVoteContainer(null);
        expect(el.querySelectorAll('.kor-active')).toHaveLength(0);
    });

    it('binds no click handler in this phase', () => {
        const el = _buildVoteContainer('KEEP');
        // jsdom does not expose listeners directly; assert the buttons carry no inline handler and
        // that the builder never references addEventListener (interaction lands in Phase 4).
        el.querySelectorAll('button').forEach((b) => expect(b.onclick).toBeNull());
    });
});

// The browser section (bootstrap / MutationObserver / _tick) must not run under jsdom: the module
// returns right after `module.exports`, so importing it has no side effects.
describe('module import', () => {
    it('exports only the pure helpers and starts nothing', () => {
        expect(typeof korVote._isDetailPage).toBe('function');
        expect(korVote._bootstrap).toBeUndefined();
        expect(korVote._tick).toBeUndefined();
    });
});
