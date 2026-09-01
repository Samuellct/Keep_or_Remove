/*
 * Keep or Remove - admin config page.
 * Read-only aggregated results table with sort + type filter, plus a "purge orphans" button, and
 * the startup-warning banner. The admin endpoints were built and proven in Phase 2; this file only
 * wires the existing markup to them.
 *
 * Pure helpers are exported for Vitest; the browser section (after the export) never runs under
 * jsdom because `module` is defined there and the IIFE returns immediately.
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
        return;
    }

    if (typeof document === 'undefined' || typeof window === 'undefined' || !window.ApiClient) {
        return;
    }

    // ---------------------------------------------------------------------------
    // Browser section - runs only on the real plugin config page.
    // ---------------------------------------------------------------------------

    function _el(id) {
        return document.getElementById(id);
    }

    function _query() {
        return {
            sort: _el('korSort').value,
            type: _el('korType').value
        };
    }

    function _renderRows(rows) {
        var body = _el('korResultsBody');
        var table = _el('korResultsTable');
        var empty = _el('korEmpty');

        if (!rows || !rows.length) {
            body.innerHTML = '';
            table.style.display = 'none';
            empty.style.display = '';
            return;
        }

        body.innerHTML = rows.map(_buildRow).join('');
        table.style.display = '';
        empty.style.display = 'none';
    }

    function _loadResults() {
        window.Dashboard.showLoadingMsg();
        return window.ApiClient.getJSON(window.ApiClient.getUrl('KeepOrRemove/admin/results', _query()))
            .then(function (rows) {
                _renderRows(rows);
            })
            .catch(function (err) {
                console.error('[KeepOrRemove Config] could not load the results:', err);
                _renderRows([]);
            })
            .then(function () {
                window.Dashboard.hideLoadingMsg();
            });
    }

    function _onShow() {
        _loadResults();
    }

    function _bind() {
        var page = _el('keepOrRemoveConfigPage');
        if (!page) {
            return;
        }
        page.addEventListener('pageshow', _onShow);
        // The selects start at total / all in the markup; re-query on every change.
        _el('korSort').addEventListener('change', _loadResults);
        _el('korType').addEventListener('change', _loadResults);
    }

    _bind();
})();
