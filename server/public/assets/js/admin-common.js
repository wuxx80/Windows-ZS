var TOKEN_KEY = 'zs_admin_token';
var USER_KEY = 'zs_admin_user';

// API request helper
function api(path, options) {
  options = options || {};
  var token = localStorage.getItem(TOKEN_KEY);
  var headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = 'Bearer ' + token;
  var opts = {
    headers: Object.assign(headers, options.headers || {}),
    method: options.method || 'GET'
  };
  if (options.body) opts.body = JSON.stringify(options.body);
  return fetch('/api/v1' + path, opts).then(function(r) { return r.json(); });
}

// Toast notification
function toast(msg, type) {
  type = type || 'info';
  var el = document.createElement('div');
  el.style.cssText = 'position:fixed;top:20px;right:20px;z-index:9999;padding:12px 20px;border-radius:8px;font-size:14px;box-shadow:0 4px 12px rgba(0,0,0,0.15);transition:all 0.3s;max-width:400px;';
  if (type === 'success') el.style.background = '#dcfce7'; el.style.color = '#16a34a';
  if (type === 'error') { el.style.background = '#fef2f2'; el.style.color = '#dc2626'; }
  if (type === 'info') { el.style.background = '#dbeafe'; el.style.color = '#2563eb'; }
  el.textContent = msg;
  document.body.appendChild(el);
  setTimeout(function() { el.style.opacity = '0'; setTimeout(function() { el.remove(); }, 300); }, 2500);
}

// Confirm dialog
function confirmDialog(msg) {
  return new Promise(function(resolve) {
    resolve(confirm(msg));
  });
}

// Format date
function fmtDate(d) {
  if (!d) return '-';
  var dt = new Date(d);
  if (isNaN(dt.getTime())) return d;
  var y = dt.getFullYear();
  var m = ('0' + (dt.getMonth() + 1)).slice(-2);
  var day = ('0' + dt.getDate()).slice(-2);
  var h = ('0' + dt.getHours()).slice(-2);
  var mi = ('0' + dt.getMinutes()).slice(-2);
  return y + '-' + m + '-' + day + ' ' + h + ':' + mi;
}

// Format file size
function fmtSize(bytes) {
  if (!bytes) return '0 B';
  bytes = Number(bytes);
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
  if (bytes < 1073741824) return (bytes / 1048576).toFixed(1) + ' MB';
  return (bytes / 1073741824).toFixed(2) + ' GB';
}

// Status badge
function statusBadge(status, text) {
  var cls = 'pending';
  if (status === 'completed' || status === 'approved' || status === 'active' || status === 'enabled' || status === 1 || status === '1') cls = 'completed';
  else if (status === 'running' || status === 'online' || status === 'publishing') cls = 'running';
  else if (status === 'failed' || status === 'blocked' || status === 'disabled' || status === 0 || status === '0') cls = 'failed';
  return '<span class="status-badge ' + cls + '">' + (text || status) + '</span>';
}

// Escape HTML
function esc(str) {
  if (!str) return '';
  return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// Pagination component
function renderPagination(total, page, pageSize, callback) {
  var totalPages = Math.ceil(total / pageSize);
  if (totalPages <= 1) return '';
  var html = '<div class="pagination">';
  html += '<span class="page-info">共 ' + total + ' 条，第 ' + page + '/' + totalPages + ' 页</span>';
  html += '<div class="page-btns">';
  if (page > 1) html += '<button class="page-btn" data-page="' + (page - 1) + '">上一页</button>';
  for (var i = Math.max(1, page - 2); i <= Math.min(totalPages, page + 2); i++) {
    html += '<button class="page-btn' + (i === page ? ' active' : '') + '" data-page="' + i + '">' + i + '</button>';
  }
  if (page < totalPages) html += '<button class="page-btn" data-page="' + (page + 1) + '">下一页</button>';
  html += '</div></div>';
  return html;
}

// Modal helper
function showModal(title, content) {
  var overlay = document.createElement('div');
  overlay.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.5);z-index:9998;display:flex;align-items:center;justify-content:center;';
  var box = document.createElement('div');
  box.style.cssText = 'background:#fff;border-radius:12px;width:90%;max-width:600px;max-height:85vh;overflow-y:auto;box-shadow:0 20px 60px rgba(0,0,0,0.3);';
  box.innerHTML = '<div style="padding:20px 24px;border-bottom:1px solid #e2e8f0;display:flex;align-items:center;justify-content:space-between;font-size:16px;font-weight:600;">' +
    '<span>' + esc(title) + '</span>' +
    '<button onclick="this.closest(\'#modalOverlay\').remove()" style="background:none;border:none;font-size:20px;cursor:pointer;color:#94a3b8;">x</button></div>' +
    '<div style="padding:20px 24px;">' + content + '</div>';
  overlay.id = 'modalOverlay';
  overlay.appendChild(box);
  overlay.addEventListener('click', function(e) { if (e.target === overlay) overlay.remove(); });
  document.body.appendChild(overlay);
  return overlay;
}

function closeModal() {
  var el = document.getElementById('modalOverlay');
  if (el) el.remove();
}