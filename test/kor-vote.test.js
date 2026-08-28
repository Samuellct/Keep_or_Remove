import { describe, it, expect } from 'vitest';
import korVote from '../src/Jellyfin.Plugin.KeepOrRemove/Web/kor-vote.js';

const { _isDetailPage, _detailItemId, _isSupportedType, _voteTargetId, _escHtml } = korVote;

describe('_isDetailPage', () => {
    it('is true for a details route', () => {
        expect(_isDetailPage('#/details?id=abc')).toBe(true);
    });

    it('is false otherwise', () => {
        expect(_isDetailPage('#/home')).toBe(false);
        expect(_isDetailPage(undefined)).toBe(false);
    });
});

describe('_detailItemId', () => {
    it('extracts the id from the hash', () => {
        expect(_detailItemId('#/details?id=abc123-def')).toBe('abc123-def');
    });

    it('returns null when absent', () => {
        expect(_detailItemId('#/home')).toBeNull();
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
    it('returns the series id for a season or episode', () => {
        expect(_voteTargetId({ Type: 'Episode', Id: 'e1', SeriesId: 's1' })).toBe('s1');
        expect(_voteTargetId({ Type: 'Season', Id: 'se1', SeriesId: 's1' })).toBe('s1');
    });

    it('returns the item id for a movie or series', () => {
        expect(_voteTargetId({ Type: 'Movie', Id: 'm1' })).toBe('m1');
        expect(_voteTargetId({ Type: 'Series', Id: 's1' })).toBe('s1');
    });
});

describe('_escHtml', () => {
    it('escapes HTML special characters', () => {
        expect(_escHtml('<b>Tom & "Jerry"</b>')).toBe('&lt;b&gt;Tom &amp; &quot;Jerry&quot;&lt;/b&gt;');
    });
});
