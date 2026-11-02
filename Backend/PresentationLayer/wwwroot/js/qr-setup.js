/* Draws the authenticator-app QR code in the browser. The secret never leaves the page. */
(function () {
  'use strict';
  var box = document.querySelector('[data-qr-uri]');
  if (!box || typeof qrcode !== 'function') return;
  try {
    var qr = qrcode(0, 'M');
    qr.addData(box.getAttribute('data-qr-uri'));
    qr.make();
    box.innerHTML = qr.createSvgTag({ cellSize: 5, margin: 2, scalable: true });
    box.hidden = false;
  } catch (e) { /* the manual key below still works */ }
})();
