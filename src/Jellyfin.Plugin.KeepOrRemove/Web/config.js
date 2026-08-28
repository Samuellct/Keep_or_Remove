/*
 * Keep or Remove - admin config page.
 * Read-only aggregated results table with sort + type filter, plus a "purge orphans" button.
 * Pure helpers are exported for vitest; the DOM wiring runs only in a browser.
 */

(function () {
    'use strict';

    var PLUGIN_ID = 'dbcf4f1f-bc0c-4681-b79a-cbd2294b2538';

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

    function _buildRow(row) {
        return '<tr>'
            + '<td>' + _escHtml(row.name) + '</td>'
            + '<td>' + _escHtml(row.type) + '</td>'
            + '<td>' + row.keep + '</td>'
            + '<td>' + row.remove + '</td>'
            + '<td>' + row.total + '</td>'
            + '</tr>';
    }

    var api = {
        _escHtml: _escHtml,
        _buildRow: _buildRow,
        PLUGIN_ID: PLUGIN_ID
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }

    if (typeof document === 'undefined' || typeof window === 'undefined' || !window.ApiClient) {
        return;
    }

    // TODO (PLAN.md Phase 5): on pageshow, read the sort/type selects, call
    // GET /KeepOrRemove/admin/results, render rows via _buildRow, wire the purge button to
    // POST /KeepOrRemove/admin/purge, and bind the StartupWarning banner from plugin config.
    void PLUGIN_ID;
})();
