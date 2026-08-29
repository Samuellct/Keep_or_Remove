import { describe, it, expect } from 'vitest';
import korVote from '../src/Jellyfin.Plugin.KeepOrRemove/Web/kor-vote.js';

const { _isDetailPage, _detailItemId, _isSupportedType, _voteTargetId, _escHtml } = korVote;

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
