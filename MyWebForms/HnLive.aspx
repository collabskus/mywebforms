<%@ Page Title="HN Live" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="HnLive.aspx.cs"
    Inherits="MyWebForms.HnLive" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

<main class="container-fluid mt-3" style="max-width:960px;">

    <%-- ═══════════════════════════════════════════════════════════════
         Page header
    ═══════════════════════════════════════════════════════════════════ --%>
    <div class="d-flex align-items-center mb-3 flex-wrap gap-2">
        <span class="hn-logo me-2"
              style="background:#ff6600;color:#fff;font-weight:bold;padding:2px 6px;font-family:monospace;">Y</span>
        <h4 class="mb-0 fw-bold me-3">HN Live</h4>

        <span id="liveBadge"
              class="badge bg-success animate__animated animate__pulse animate__infinite"
              style="font-size:.65rem;">LIVE</span>

        <span id="statusMsg" class="text-muted small ms-2">Connecting&hellip;</span>

        <div class="ms-auto d-flex align-items-center gap-2 flex-wrap">
            <%-- Min-comments filter --%>
            <label class="form-label mb-0 small" for="minComments">Min comments:</label>
            <input type="number" id="minComments" class="form-control form-control-sm"
                   style="width:70px;" value="0" min="0" max="9999" />

            <%-- Min-points filter --%>
            <label class="form-label mb-0 small" for="minPoints">Min points:</label>
            <input type="number" id="minPoints" class="form-control form-control-sm"
                   style="width:70px;" value="0" min="0" max="9999" />

            <button id="btnPause" class="btn btn-sm btn-outline-secondary">⏸ Pause</button>
            <button id="btnClear" class="btn btn-sm btn-outline-danger">🗑 Clear</button>
        </div>
    </div>

    <%-- ═══════════════════════════════════════════════════════════════
         Counter bar
    ═══════════════════════════════════════════════════════════════════ --%>
    <div class="d-flex gap-3 mb-3 small text-muted">
        <span>Items seen: <strong id="countSeen">0</strong></span>
        <span>Shown: <strong id="countShown">0</strong></span>
        <span>Filtered out: <strong id="countFiltered">0</strong></span>
    </div>

    <%-- ═══════════════════════════════════════════════════════════════
         Live feed container — items prepended here by JS
    ═══════════════════════════════════════════════════════════════════ --%>
    <div id="hnFeed"></div>

    <%-- Loading spinner shown while the first batch loads --%>
    <div id="loadingSpinner" class="text-center py-5">
        <div class="spinner-border text-warning" role="status">
            <span class="visually-hidden">Loading…</span>
        </div>
        <p class="mt-2 text-muted small">Fetching latest items from HN&hellip;</p>
    </div>

</main>

<style>
    /* ── Live feed items ─────────────────────────────────────────────── */
    .hn-live-item {
        border-bottom: 1px solid #f0f0f0;
        padding: .55rem .25rem;
        animation: hnFadeIn .4s ease-in-out;
    }
    @keyframes hnFadeIn {
        from { opacity: 0; transform: translateY(-6px); }
        to   { opacity: 1; transform: translateY(0); }
    }
    .hn-live-item:hover { background: #fffaf5; }
    .hn-live-title { font-size: .93rem; font-weight: 500; }
    .hn-live-title a { color: #000; text-decoration: none; }
    .hn-live-title a:hover { color: #ff6600; }
    .hn-live-meta { font-size: .75rem; color: #666; }
    .hn-live-meta a { color: inherit; text-decoration: underline dotted; }
    .hn-live-score { color: #ff6600; font-weight: 600; }
    .hn-type-comment .hn-live-title a { color: #555; font-weight: 400; font-style: italic; }
    .hn-type-job     { background: #fffff0; }
</style>

<%-- ═══════════════════════════════════════════════════════════════════
     Page-specific script — pure client-side live feed
     Strategy:
       1. On load, fetch /v0/maxitem.json to get the current high-water mark.
       2. Back-fill the last BackfillCount items to seed the feed.
       3. Every PollMs, fetch maxitem again; for any new IDs since last seen,
          fetch each item concurrently and prepend to the feed.
       4. The minComments / minPoints inputs gate which items are shown.
       5. Pause button stops new fetches without losing position.
═══════════════════════════════════════════════════════════════════════ --%>
<script>
(function () {
    'use strict';

    var HN_BASE    = 'https://hacker-news.firebaseio.com/v0/';
    var POLL_MS    = 6000;          // poll interval
    var BACKFILL   = 30;            // items to back-fill on first load
    var MAX_FEED   = 300;           // max DOM nodes kept in feed before pruning

    var lastMaxId  = 0;
    var paused     = false;
    var countSeen  = 0;
    var countShown = 0;
    var countFilt  = 0;
    var pollTimer  = null;

    var feed       = document.getElementById('hnFeed');
    var spinner    = document.getElementById('loadingSpinner');
    var statusEl   = document.getElementById('statusMsg');
    var seenEl     = document.getElementById('countSeen');
    var shownEl    = document.getElementById('countShown');
    var filtEl     = document.getElementById('countFiltered');
    var btnPause   = document.getElementById('btnPause');
    var btnClear   = document.getElementById('btnClear');
    var inMinCmt   = document.getElementById('minComments');
    var inMinPts   = document.getElementById('minPoints');

    // ── Counters ─────────────────────────────────────────────────────

    function updateCounters() {
        seenEl.textContent  = countSeen;
        shownEl.textContent = countShown;
        filtEl.textContent  = countFilt;
    }

    // ── Fetch helpers ─────────────────────────────────────────────────

    function fetchJson(url) {
        return fetch(url).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    function fetchItem(id) {
        return fetchJson(HN_BASE + 'item/' + id + '.json');
    }

    function fetchMaxItem() {
        return fetchJson(HN_BASE + 'maxitem.json');
    }

    // ── Filtering ─────────────────────────────────────────────────────

    function passesFilter(item) {
        if (!item || item.deleted || item.dead) return false;
        var minC = parseInt(inMinCmt.value, 10) || 0;
        var minP = parseInt(inMinPts.value,  10) || 0;
        var comments = item.descendants || 0;
        var score    = item.score       || 0;
        if (minC > 0 && minP > 0) return comments >= minC || score >= minP;
        if (minC > 0) return comments >= minC;
        if (minP > 0) return score    >= minP;
        return true;
    }

    // ── Rendering ─────────────────────────────────────────────────────

    function itemTypeClass(item) {
        if (!item.type) return '';
        return 'hn-type-' + item.type;
    }

    function renderItem(item, prepend) {
        countSeen++;

        if (!passesFilter(item)) {
            countFilt++;
            updateCounters();
            return;
        }
        countShown++;
        updateCounters();

        var el       = document.createElement('div');
        el.className = 'hn-live-item ' + itemTypeClass(item);

        var title, url, meta;

        if (item.type === 'comment') {
            // Comments: show a snippet of the text and link to parent story
            var snippet = '';
            if (item.text) {
                var tmp = document.createElement('div');
                tmp.innerHTML = item.text;
                snippet = (tmp.textContent || tmp.innerText || '').substring(0, 120);
                if (snippet.length === 120) snippet += '…';
            }
            title = '"' + (snippet || '(empty comment)') + '"';
            url   = 'https://news.ycombinator.com/item?id=' + item.id;
            meta  = 'comment by <a href="https://news.ycombinator.com/user?id='
                  + esc(item.by || '[deleted]') + '" target="_blank">'
                  + esc(item.by || '[deleted]') + '</a>'
                  + (item.parent ? ' &nbsp;|&nbsp; <a href="https://news.ycombinator.com/item?id='
                  + item.parent + '" target="_blank">parent</a>' : '')
                  + ' &nbsp;|&nbsp; ' + timeAgo(item.time);
        } else {
            // story / job / poll
            title = esc(item.title || '(untitled)');
            url   = item.url || ('https://news.ycombinator.com/item?id=' + item.id);
            var score = item.score || 0;
            var desc  = item.descendants || 0;
            meta  = '<span class="hn-live-score">▲ ' + score + ' pts</span>'
                  + ' &nbsp;by <a href="https://news.ycombinator.com/user?id='
                  + esc(item.by || '[deleted]') + '" target="_blank">'
                  + esc(item.by || '[deleted]') + '</a>'
                  + ' &nbsp;|&nbsp; '
                  + '<a href="https://news.ycombinator.com/item?id=' + item.id
                  + '" target="_blank">' + desc + ' comment' + (desc !== 1 ? 's' : '') + '</a>'
                  + ' &nbsp;|&nbsp; ' + timeAgo(item.time)
                  + ' &nbsp;|&nbsp; <span class="badge bg-secondary" style="font-size:.65rem;">'
                  + esc(item.type || 'story') + '</span>';
        }

        el.innerHTML =
            '<div class="hn-live-title"><a href="' + esc(url) + '" target="_blank">'
            + title + '</a></div>'
            + '<div class="hn-live-meta">' + meta + '</div>';

        if (prepend && feed.firstChild) {
            feed.insertBefore(el, feed.firstChild);
        } else {
            feed.appendChild(el);
        }

        // Prune excess nodes to avoid infinite DOM growth
        while (feed.childNodes.length > MAX_FEED) {
            feed.removeChild(feed.lastChild);
        }
    }

    function esc(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function timeAgo(unix) {
        if (!unix) return '';
        var diff = Math.floor(Date.now() / 1000) - unix;
        if (diff <  60)  return diff + 's ago';
        if (diff <  3600) return Math.floor(diff / 60) + 'm ago';
        if (diff < 86400) return Math.floor(diff / 3600) + 'h ago';
        return Math.floor(diff / 86400) + 'd ago';
    }

    // ── Back-fill ─────────────────────────────────────────────────────
    // Fetch the most recent BACKFILL items in descending order and append
    // them to the feed (oldest first = natural reading order).

    function backfill(maxId) {
        var ids = [];
        for (var i = 0; i < BACKFILL; i++) {
            if (maxId - i > 0) ids.push(maxId - i);
        }

        var promises = ids.map(function (id) { return fetchItem(id); });

        Promise.all(promises).then(function (items) {
            // Sort oldest-first before appending
            items.sort(function (a, b) {
                var at = (a && a.time) || 0;
                var bt = (b && b.time) || 0;
                return at - bt;
            });
            spinner.style.display = 'none';
            items.forEach(function (item) {
                if (item) renderItem(item, false);
            });
            setStatus('Watching for new items…');
            schedulePoll();
        }).catch(function (err) {
            setStatus('Error during back-fill: ' + err.message);
            schedulePoll();
        });
    }

    // ── Poll loop ─────────────────────────────────────────────────────

    function poll() {
        if (paused) return;

        fetchMaxItem().then(function (newMax) {
            if (newMax <= lastMaxId) {
                setStatus('Up to date — watching…');
                schedulePoll();
                return;
            }

            // Clamp burst to 50 items to avoid flooding
            var start = lastMaxId + 1;
            var end   = Math.min(newMax, lastMaxId + 50);
            lastMaxId = newMax;

            var ids = [];
            for (var id = end; id >= start; id--) ids.push(id);

            var promises = ids.map(function (id) { return fetchItem(id); });

            Promise.all(promises).then(function (items) {
                // Newest first (already descending) — prepend so newest is at top
                items.forEach(function (item) {
                    if (item) renderItem(item, true);
                });
                setStatus('Last update: ' + new Date().toLocaleTimeString());
                schedulePoll();
            }).catch(function () {
                schedulePoll();
            });
        }).catch(function (err) {
            setStatus('Poll error: ' + err.message);
            schedulePoll();
        });
    }

    function schedulePoll() {
        if (pollTimer) clearTimeout(pollTimer);
        pollTimer = setTimeout(poll, POLL_MS);
    }

    function setStatus(msg) {
        if (statusEl) statusEl.textContent = msg;
    }

    // ── UI controls ───────────────────────────────────────────────────

    btnPause.addEventListener('click', function () {
        paused = !paused;
        btnPause.textContent = paused ? '▶ Resume' : '⏸ Pause';
        btnPause.className   = paused
            ? 'btn btn-sm btn-warning'
            : 'btn btn-sm btn-outline-secondary';
        document.getElementById('liveBadge').style.opacity = paused ? '.3' : '1';
        if (!paused) { setStatus('Resuming…'); poll(); }
        else          { setStatus('Paused'); }
    });

    btnClear.addEventListener('click', function () {
        feed.innerHTML = '';
        countSeen = countShown = countFilt = 0;
        updateCounters();
    });

    // ── Boot ──────────────────────────────────────────────────────────

    setStatus('Fetching latest item ID…');

    fetchMaxItem().then(function (maxId) {
        lastMaxId = maxId;
        setStatus('Back-filling last ' + BACKFILL + ' items…');
        backfill(maxId);
    }).catch(function (err) {
        spinner.style.display = 'none';
        setStatus('Failed to connect: ' + err.message);
    });

}());
</script>

</asp:Content>
