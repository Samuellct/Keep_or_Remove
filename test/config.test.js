import { describe, it, expect } from 'vitest';
import korConfig from '../src/Jellyfin.Plugin.KeepOrRemove/Web/config.js';

const { _escHtml, _buildRow, _purgeMessage, PLUGIN_ID } = korConfig;

describe('_escHtml', () => {
    it('escapes HTML special characters', () => {
        expect(_escHtml('<b>Tom & "Jerry"</b>')).toBe('&lt;b&gt;Tom &amp; &quot;Jerry&quot;&lt;/b&gt;');
    });

    it('returns an empty string for nullish input', () => {
        expect(_escHtml(null)).toBe('');
        expect(_escHtml(undefined)).toBe('');
    });
});

describe('_buildRow', () => {
    it('renders five cells: escaped name and type, raw counts', () => {
        const html = _buildRow({ name: 'Ad Astra', type: 'Movie', keep: 4, remove: 1, total: 5 });
        expect(html).toBe('<tr><td>Ad Astra</td><td>Movie</td><td>4</td><td>1</td><td>5</td></tr>');
    });

    it('escapes a name that contains markup so a media title cannot inject HTML', () => {
        const html = _buildRow({
            name: '<script>alert(1)</script> & "quotes"',
            type: 'Series',
            keep: 0,
            remove: 2,
            total: 2
        });
        expect(html).toContain('&lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;quotes&quot;');
        expect(html).not.toContain('<script>');
    });

    it('passes an orphan row through with its Unknown type', () => {
        const html = _buildRow({
            name: '00000000-0000-0000-0000-0000deadbeef',
            type: 'Unknown',
            keep: 1,
            remove: 0,
            total: 1
        });
        expect(html).toContain('<td>00000000-0000-0000-0000-0000deadbeef</td>');
        expect(html).toContain('<td>Unknown</td>');
    });
});

describe('_purgeMessage', () => {
    it('phrases the removed count', () => {
        expect(_purgeMessage(0)).toBe('0 orphan vote(s) removed.');
        expect(_purgeMessage(1)).toBe('1 orphan vote(s) removed.');
        expect(_purgeMessage(2)).toBe('2 orphan vote(s) removed.');
    });
});

describe('PLUGIN_ID', () => {
    it('is the plugin GUID', () => {
        expect(PLUGIN_ID).toBe('dbcf4f1f-bc0c-4681-b79a-cbd2294b2538');
    });
});

// The browser section (pageshow bind / load / purge / warning) must not run under jsdom: the module
// returns right after `module.exports`, so importing it has no side effects.
describe('module import', () => {
    it('exports only the pure helpers and starts nothing', () => {
        expect(typeof korConfig._buildRow).toBe('function');
        expect(korConfig._bind).toBeUndefined();
        expect(korConfig._loadResults).toBeUndefined();
        expect(korConfig._onPurge).toBeUndefined();
        expect(korConfig._loadWarning).toBeUndefined();
    });
});
