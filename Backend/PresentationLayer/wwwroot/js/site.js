/* Peniel Place Ministries - front-end behaviour.
   Everything here is progressive: the forms still work (and the server still validates) without it. */
(function () {
  'use strict';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

  /* ---- Show / hide password ---- */
  $$('[data-toggle-password]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var input = $(btn.getAttribute('data-toggle-password'));
      if (!input) return;
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      btn.textContent = show ? 'Hide' : 'Show';
      btn.setAttribute('aria-pressed', String(show));
    });
  });

  /* ---- Print buttons ---- */
  $$('[data-print]').forEach(function (b) { b.addEventListener('click', function () { window.print(); }); });

  /* ---- Digits only in the 2FA box ---- */
  var code = $('.code-input');
  if (code) code.addEventListener('input', function () { code.value = code.value.replace(/\D/g, '').slice(0, 6); });

  /* ---- Donation capture form ---- */
  var form = $('#donation-form');
  if (form) initDonationForm(form);

  function setMsg(name, text) {
    var span = $('[data-valmsg-for="' + name + '"]');
    if (span) { span.textContent = text || ''; }
  }

  function initDonationForm(form) {
    var inKindField = $('#IsInKind', form);
    var monetary = $('#monetary-fields');
    var inKind = $('#inkind-fields');
    var donorId = $('#DonorId', form);
    var donorName = $('#DonorName', form);
    var anon = $('#IsAnonymous', form);
    var search = $('#donor-search');
    var results = $('#donor-results');
    var empty = $('#donor-empty');
    var picker = $('#donor-picker');
    var selected = $('#donor-selected');
    var selectedName = $('#donor-selected-name');

    /* Gift type decides which fields are shown */
    function applyCategory() {
      var chosen = $('input[name="CategoryId"]:checked', form);
      var isKind = !!chosen && chosen.getAttribute('data-inkind') === 'true';
      inKindField.value = isKind ? 'true' : 'false';
      monetary.hidden = !chosen || isKind;
      inKind.hidden = !isKind;
      if (!chosen) { monetary.hidden = false; }
    }
    $$('input[name="CategoryId"]', form).forEach(function (r) { r.addEventListener('change', applyCategory); });
    applyCategory();

    /* Donor picker */
    function showSelected(id, name) {
      donorId.value = id; donorName.value = name;
      selectedName.textContent = name;
      selected.hidden = false; picker.hidden = true; results.hidden = true; empty.hidden = true;
      anon.checked = false; setMsg('DonorId', '');
    }
    function clearSelected() {
      donorId.value = ''; donorName.value = '';
      selected.hidden = true; picker.hidden = false; search.value = ''; search.focus();
    }
    $('#donor-clear').addEventListener('click', clearSelected);
    anon.addEventListener('change', function () {
      if (anon.checked) { donorId.value = ''; donorName.value = ''; selected.hidden = true; picker.hidden = true; setMsg('DonorId', ''); }
      else { picker.hidden = false; }
    });
    if (donorId.value && donorName.value) showSelected(donorId.value, donorName.value);
    else if (anon.checked) picker.hidden = true;

    var timer;
    search.addEventListener('input', function () {
      clearTimeout(timer);
      var term = search.value.trim();
      if (term.length < 2) { results.hidden = true; empty.hidden = true; return; }
      timer = setTimeout(function () { lookup(term); }, 250);
    });
    function lookup(term) {
      fetch(search.getAttribute('data-search-url') + '?term=' + encodeURIComponent(term), { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
        .then(function (r) { if (!r.ok) throw new Error('lookup failed'); return r.json(); })
        .then(function (items) {
          results.innerHTML = '';
          if (!items.length) { results.hidden = true; empty.hidden = false; return; }
          empty.hidden = true;
          items.slice(0, 8).forEach(function (d) {
            var li = document.createElement('li'); li.setAttribute('role', 'option');
            var b = document.createElement('button'); b.type = 'button';
            b.textContent = d.name;
            if (d.detail) { var s = document.createElement('small'); s.textContent = d.detail; b.appendChild(s); }
            b.addEventListener('click', function () { showSelected(d.id, d.name); });
            li.appendChild(b); results.appendChild(li);
          });
          results.hidden = false;
        })
        .catch(function () {
          results.hidden = true; empty.hidden = false;
          empty.textContent = 'Could not search right now. Tick "Anonymous or donor not known" and fix the donor later, or try again.';
        });
    }

    /* Checks that depend on the gift type (the server repeats them) */
    function conditionalOk() {
      var ok = true;
      setMsg('Amount', ''); setMsg('InKindDescription', ''); setMsg('DonorId', '');
      if (!$('input[name="CategoryId"]:checked', form)) { setMsg('CategoryId', 'Choose the type of gift.'); ok = false; }
      if (inKindField.value === 'true') {
        if (!$('#InKindDescription').value.trim()) { setMsg('InKindDescription', "Describe what was donated, like '5 bags of maize meal'."); ok = false; }
      } else {
        var raw = $('#Amount').value.trim();
        var n = Number(raw);
        if (!raw) { setMsg('Amount', 'Enter the amount received.'); ok = false; }
        else if (!isFinite(n) || n < 0.01 || n > 10000000) { setMsg('Amount', 'Enter an amount between R 0.01 and R 10 000 000, using numbers only.'); ok = false; }
      }
      if (!donorId.value && !anon.checked) { setMsg('DonorId', "Pick a donor, or tick 'Anonymous or donor not known'."); ok = false; }
      return ok;
    }

    /* Submit: validate, queue if offline, otherwise guard against double taps */
    form.addEventListener('submit', function (e) {
      if (!conditionalOk()) { e.preventDefault(); var bad = $('.field-error:not(:empty)'); if (bad) bad.scrollIntoView({ block: 'center' }); return; }
      if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
      if (!navigator.onLine) {
        e.preventDefault();
        if (enqueue(form)) { showPending(); form.reset(); applyCategory(); clearSelected(); }
        else alert('This device could not save the donation offline. Please write it down and enter it when you are back online.');
        return;
      }
    });
  }

  /* ---- Loading state on every submit button (stops double submits) ---- */
  $$('form').forEach(function (f) {
    f.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      if (window.jQuery && jQuery(f).valid && !jQuery(f).valid()) return;
      $$('button[type=submit][data-loading-text]', f).forEach(function (b) {
        setTimeout(function () { b.disabled = true; b.textContent = b.getAttribute('data-loading-text'); }, 0);
      });
    });
  });
  window.addEventListener('pageshow', function (e) {            // back button re-enables buttons
    if (e.persisted) $$('button[type=submit][disabled]').forEach(function (b) { b.disabled = false; });
  });

  /* ---- Offline queue: nothing is lost if the signal drops at an outreach event ---- */
  var KEY = 'pp_pending_donations';
  function readQ() { try { return JSON.parse(localStorage.getItem(KEY) || '[]'); } catch (e) { return []; } }
  function writeQ(q) { try { localStorage.setItem(KEY, JSON.stringify(q)); return true; } catch (e) { return false; } }
  function enqueue(f) {
    var pairs = []; new FormData(f).forEach(function (v, k) { pairs.push([k, String(v)]); });
    var q = readQ(); q.push({ action: f.action, pairs: pairs, savedAt: new Date().toISOString() });
    return writeQ(q);
  }
  function showPending() {
    var box = $('#pending-banner'); if (!box) return;
    var n = readQ().length;
    box.hidden = n === 0;
    if (n) box.innerHTML = '<strong>' + n + ' donation' + (n === 1 ? '' : 's') + ' saved on this device.</strong> ' +
      (navigator.onLine ? 'Sending now...' : 'They will be sent when you are back online.');
  }
  function syncQueue() {
    var q = readQ(); if (!q.length || !navigator.onLine) return;
    var item = q[0];
    var body = new URLSearchParams(); item.pairs.forEach(function (p) { body.append(p[0], p[1]); });
    fetch(item.action, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Offline-Sync': '1' }, body: body })
      .then(function (r) {
        /* Signed out meanwhile: the server redirected to the login page (HTTP 200). Keep the donation queued. */
        if (r.redirected && /\/Account\/(Login|TwoFactor|SetupAuthenticator)/i.test(r.url)) return;
        if (r.ok) { var cur = readQ(); cur.shift(); writeQ(cur); showPending(); if (cur.length) syncQueue(); else { var b = $('#pending-banner'); if (b) { b.hidden = false; b.innerHTML = '<strong>All saved donations have been sent.</strong>'; } } }
        else if (r.status === 400) { var c = readQ(); var bad = c.shift(); bad.rejected = true; localStorage.setItem('pp_rejected_donations', JSON.stringify((JSON.parse(localStorage.getItem('pp_rejected_donations') || '[]')).concat([bad]))); writeQ(c); var bx = $('#pending-banner'); if (bx) { bx.hidden = false; bx.innerHTML = '<strong>A saved donation was rejected by the server</strong> (for example a missing field). Please record it again.'; } }
        /* any other status (401, 5xx): keep it queued and try again later */
      })
      .catch(function () { /* still offline: keep queued */ });
  }
  function updateOnline() {
    var banner = $('#offline-banner'); if (banner) banner.hidden = navigator.onLine;
    showPending(); if (navigator.onLine) syncQueue();
  }
  window.addEventListener('online', updateOnline);
  window.addEventListener('offline', updateOnline);
  if ($('#donation-form')) updateOnline();

  /* ---- Installable app shell ---- */
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () { navigator.serviceWorker.register('/service-worker.js').catch(function () {}); });
  }
})();
