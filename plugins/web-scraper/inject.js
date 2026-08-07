/*
 * 网页爬虫插件（LfsunBrowser 自定义插件格式）—— 可视化选点版
 * 不懂代码也能爬虫：悬浮高亮 → 点击选取 → 自动识别同类 → 实时预览 → 导出
 */
(function () {
  if (window.__webScraperInstalled) return;
  window.__webScraperInstalled = true;

  var PLUGIN_ID = 'web-scraper';

  function getChrome() {
    try { if (window.__lfsunChromeFor) return window.__lfsunChromeFor(PLUGIN_ID); } catch (e) {}
    return window.chrome || {};
  }
  var chromeApi = getChrome();

  // ============ 工具函数 ============
  function ce(tag, cls, html) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (html != null) n.innerHTML = html;
    return n;
  }
  function $(sel, root) { return (root || document).querySelector(sel); }
  function cssEscape(s) {
    return String(s).replace(/[^a-zA-Z0-9_-]/g, function (c) { return '\\' + c.charCodeAt(0).toString(16) + ' '; });
  }

  // ============ 智能选择器生成 ============
  function buildCssSelector(el) {
    if (!el || el.nodeType !== 1) return '';
    if (el.id && /^[a-zA-Z]/.test(el.id)) return '#' + cssEscape(el.id);
    var parts = [];
    var node = el;
    var depth = 0;
    while (node && node.nodeType === 1 && node !== document.documentElement && depth < 6) {
      var part = node.tagName.toLowerCase();
      if (node.id && /^[a-zA-Z]/.test(node.id)) {
        parts.unshift('#' + cssEscape(node.id));
        break;
      }
      if (node.classList && node.classList.length) {
        var stable = [];
        for (var i = 0; i < node.classList.length; i++) {
          var c = node.classList[i];
          if (c && c.length > 2 && !/^[0-9]/.test(c) && !/__/.test(c) && c.indexOf('__') === -1) {
            stable.push(cssEscape(c));
            if (stable.length >= 2) break;
          }
        }
        if (stable.length) part += '.' + stable.join('.');
      }
      var parent = node.parentElement;
      if (parent) {
        var siblings = Array.prototype.filter.call(parent.children, function (c) { return c.tagName === node.tagName; });
        if (siblings.length > 1) {
          var idx = Array.prototype.indexOf.call(parent.children, node) + 1;
          part += ':nth-child(' + idx + ')';
        }
      }
      parts.unshift(part);
      node = parent;
      depth++;
    }
    return parts.join(' > ');
  }

  // 验证选择器是否唯一命中目标元素
  function validateSelector(sel, target) {
    try {
      var matches = document.querySelectorAll(sel);
      return matches.length === 1 && matches[0] === target;
    } catch (e) { return false; }
  }

  // 简化选择器：尽量短但仍唯一
  function simplifySelector(sel, target) {
    if (sel.indexOf('#') >= 0) return sel;
    var parts = sel.split(' > ');
    // 从最右开始尝试缩短
    for (var start = 0; start < parts.length - 1; start++) {
      var short = parts.slice(start).join(' > ');
      if (validateSelector(short, target)) {
        return short;
      }
    }
    return sel;
  }

  // 中文注释：生成 fieldEl 相对 listItemEl 的子路径（纯 tag.class，不含 nth-child，可在每条列表项内复用）。
  function getFieldSelector(fieldEl, listItemEl) {
    if (!fieldEl || fieldEl === listItemEl) return '';
    var parts = [];
    var node = fieldEl;
    var depth = 0;
    while (node && node !== listItemEl && node.nodeType === 1 && depth < 8) {
      var part = node.tagName.toLowerCase();
      if (node.classList && node.classList.length && typeof node.className === 'string') {
        var cls = node.className.trim().split(/\s+/).slice(0, 3).filter(function (c) { return c.length > 1 && c.length < 40; }).map(cssEscape).join('.');
        if (cls) part += '.' + cls;
      }
      parts.unshift(part);
      node = node.parentElement;
      depth++;
    }
    return parts.join(' > ');
  }

  // 中文注释：生成列表容器选择器（唯一标识装着一组列表项的父元素）。
  function buildContainerSelector(el) {
    if (!el || el === document.body) return 'body';
    if (el.id && /^[a-zA-Z]/.test(el.id)) return '#' + cssEscape(el.id);
    var tag = el.tagName.toLowerCase();
    var cls = '';
    if (el.classList && el.classList.length && typeof el.className === 'string') {
      var fc = el.className.trim().split(/\s+/).filter(function (c) { return c.length > 1 && c.length < 40; })[0] || '';
      if (fc) cls = '.' + cssEscape(fc);
    }
    if (document.querySelectorAll(tag + cls).length === 1) return tag + cls;
    return simplifySelector(buildCssSelector(el), el);
  }

  // 中文注释：向上查找包含 >=2 个同类子元素的父元素，作为列表容器。
  function findListItems(el) {
    var tag = el.tagName.toLowerCase();
    var firstClass = '';
    if (el.classList && el.classList.length && typeof el.className === 'string') {
      firstClass = el.className.trim().split(/\s+/).filter(function (c) { return c.length > 1 && c.length < 40; })[0] || '';
    }
    var parent = el.parentElement;
    var best = null;
    while (parent && parent !== document.body && parent !== document.documentElement) {
      var matches = Array.prototype.filter.call(parent.children, function (c) {
        if (c.tagName.toLowerCase() !== tag) return false;
        if (firstClass && !c.classList.contains(firstClass)) return false;
        return true;
      });
      if (matches.length >= 3) {
        return { parent: parent, items: matches, containerSelector: buildContainerSelector(parent) };
      }
      if (matches.length >= 2 && !best) {
        best = { parent: parent, items: matches, containerSelector: buildContainerSelector(parent) };
      }
      parent = parent.parentElement;
    }
    if (best) return best;
    return { parent: el.parentElement || document.body, items: [el], containerSelector: buildContainerSelector(el.parentElement || document.body) };
  }

  // ============ 元素高亮 ============
  var highlightStyle = null;
  var overlayBox = null;
  var highlightedEl = null;
  var hoverPreview = null;

  function ensureStyles() {
    if (highlightStyle) return;
    highlightStyle = ce('style');
    highlightStyle.textContent = `
      .__ws-highlight { outline: 2px solid #1976d2 !important; outline-offset: 1px !important; background: rgba(25,118,210,0.06) !important; cursor: crosshair !important; transition: outline-color .12s, background .12s !important; }
      .__ws-highlight-container { outline: 2px dashed #e65100 !important; outline-offset: 2px !important; background: rgba(230,81,0,0.04) !important; }
      .__ws-highlight-field { outline: 2px solid #43a047 !important; outline-offset: 1px !important; background: rgba(67,160,71,0.08) !important; }
      .__ws-panel { position: fixed; top: 0; right: 0; width: 360px; height: 100vh; background: #fff; box-shadow: -2px 0 12px rgba(0,0,0,0.15); z-index: 2147483600; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; font-size: 13px; color: #333; display: flex; flex-direction: column; transition: transform .25s; }
      .__ws-panel-hidden { transform: translateX(360px); }
      .__ws-panel-header { background: #1976d2; color: #fff; padding: 12px 16px; font-weight: 600; font-size: 14px; display: flex; align-items: center; justify-content: space-between; flex-shrink: 0; }
      .__ws-panel-close { background: none; border: none; color: #fff; font-size: 20px; cursor: pointer; padding: 0 4px; line-height: 1; }
      .__ws-panel-tabs { display: flex; border-bottom: 1px solid #e0e0e0; flex-shrink: 0; background: #f5f5f5; }
      .__ws-tab { flex: 1; padding: 10px 8px; text-align: center; cursor pointer; border-bottom: 2px solid transparent; font-size: 12px; transition: all .15s; color: #666; }
      .__ws-tab-active { border-bottom-color: #1976d2; color: #1976d2; font-weight: 600; background: #fff; }
      .__ws-panel-body { flex: 1; overflow-y: auto; padding: 12px 16px; }
      .__ws-panel-footer { padding: 10px 16px; border-top: 1px solid #e0e0e0; display: flex; gap: 8px; flex-shrink: 0; background: #fafafa; }
      .__ws-btn { padding: 8px 16px; border-radius: 4px; border: 1px solid #ccc; background: #fff; cursor: pointer; font-size: 13px; transition: all .15s; }
      .__ws-btn:hover { background: #f0f0f0; }
      .__ws-btn-primary { background: #1976d2; color: #fff; border-color: #1976d2; }
      .__ws-btn-primary:hover { background: #1565c0; }
      .__ws-btn-success { background: #43a047; color: #fff; border-color: #43a047; }
      .__ws-btn-success:hover { background: #388e3c; }
      .__ws-btn-danger { background: #e53935; color: #fff; border-color: #e53935; }
      .__ws-btn-active { background: #e65100 !important; color: #fff !important; border-color: #e65100 !important; }
      .__ws-btn:disabled { opacity: 0.5; cursor: default; }
      .__ws-empty { text-align: center; color: #999; padding: 30px 10px; font-size: 13px; }
      .__ws-empty-icon { font-size: 36px; margin-bottom: 12px; opacity: 0.4; }
      .__ws-field-card { border: 1px solid #e0e0e0; border-radius: 6px; padding: 10px; margin-bottom: 8px; background: #fafafa; }
      .__ws-field-card:hover { border-color: #1976d2; }
      .__ws-field-label { font-weight: 600; color: #1976d2; margin-bottom: 4px; font-size: 12px; }
      .__ws-field-sel { font-family: monospace; font-size: 11px; color: #666; word-break: break-all; margin-bottom: 4px; }
      .__ws-field-preview { font-size: 12px; color: #333; background: #fff; padding: 6px 8px; border-radius: 3px; border: 1px solid #eee; max-height: 60px; overflow: hidden; }
      .__ws-badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; color: #fff; }
      .__ws-badge-info { background: #1976d2; }
      .__ws-badge-warn { background: #e65100; }
      .__ws-badge-ok { background: #43a047; }
      .__ws-steps { display: flex; align-items: center; gap: 4px; margin-bottom: 16px; font-size: 12px; }
      .__ws-step { display: flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 12px; background: #eee; color: #999; }
      .__ws-step-active { background: #1976d2; color: #fff; }
      .__ws-step-done { background: #43a047; color: #fff; }
      .__ws-step-arrow { color: #ccc; }
      .__ws-table { width: 100%; border-collapse: collapse; font-size: 12px; }
      .__ws-table th { background: #f5f5f5; padding: 6px 8px; text-align: left; border: 1px solid #e0e0e0; position: sticky; top: 0; }
      .__ws-table td { padding: 5px 8px; border: 1px solid #e0e0e0; max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .__ws-table tr:nth-child(even) td { background: #fafafa; }
      .__ws-toolbar-floating { position: fixed; bottom: 24px; right: 24px; z-index: 2147483601; }
      .__ws-fab { width: 48px; height: 48px; border-radius: 50%; background: #1976d2; color: #fff; border: none; box-shadow: 0 4px 12px rgba(0,0,0,0.25); cursor: pointer; font-size: 22px; transition: all .2s; display: flex; align-items: center; justify-content: center; }
      .__ws-fab:hover { background: #1565c0; transform: scale(1.05); }
      .__ws-toast { position: fixed; top: 20px; left: 50%; transform: translateX(-50%); background: #333; color: #fff; padding: 10px 20px; border-radius: 6px; z-index: 2147483602; font-size: 13px; box-shadow: 0 4px 12px rgba(0,0,0,0.3); animation: __ws-toast-in .3s; }
      @keyframes __ws-toast-in { from { opacity: 0; transform: translateX(-50%) translateY(-10px); } to { opacity: 1; transform: translateX(-50%) translateY(0); } }
      .__ws-row-card { border: 1px solid #e0e0e0; border-radius: 4px; padding: 8px; margin-bottom: 6px; font-size: 12px; }
      .__ws-row-label { color: #999; margin-bottom: 3px; }
      .__ws-row-val { color: #333; }
    `;
    document.head.appendChild(highlightStyle);

    overlayBox = ce('div');
    overlayBox.style.cssText = 'position:absolute;pointer-events:none;z-index:2147483601;border:2px solid #1976d2;background:rgba(25,118,210,0.08);transition:all .08s;';
    document.body.appendChild(overlayBox);

    hoverPreview = ce('div');
    hoverPreview.style.cssText = 'position:fixed;z-index:2147483602;background:#333;color:#fff;padding:6px 10px;border-radius:4px;font-size:11px;pointer-events:none;max-width:280px;word-break:break-all;display:none;font-family:monospace;';
    document.body.appendChild(hoverPreview);
  }

  function showHighlight(el, cls) {
    clearHighlight();
    if (!el || el.nodeType !== 1) return;
    highlightedEl = el;
    el.classList.add(cls || '__ws-highlight');
    cls = cls || '__ws-highlight';
    var rect = el.getBoundingClientRect();
    overlayBox.className = '';
    overlayBox.className = cls;
    overlayBox.style.left = (rect.left + window.scrollX) + 'px';
    overlayBox.style.top = (rect.top + window.scrollY) + 'px';
    overlayBox.style.width = rect.width + 'px';
    overlayBox.style.height = rect.height + 'px';
    overlayBox.style.display = 'block';
  }

  function clearHighlight() {
    if (highlightedEl) {
      highlightedEl.classList.remove('__ws-highlight');
      highlightedEl.classList.remove('__ws-highlight-container');
      highlightedEl.classList.remove('__ws-highlight-field');
      highlightedEl = null;
    }
    overlayBox.style.display = 'none';
  }

  function showHoverPreview(text, x, y) {
    hoverPreview.textContent = text;
    hoverPreview.style.display = 'block';
    hoverPreview.style.left = (x + 14) + 'px';
    hoverPreview.style.top = (y + 14) + 'px';
    var rect = hoverPreview.getBoundingClientRect();
    if (rect.right > window.innerWidth) hoverPreview.style.left = (x - rect.width - 14) + 'px';
    if (rect.bottom > window.innerHeight) hoverPreview.style.top = (y - rect.height - 14) + 'px';
  }

  function hideHoverPreview() {
    hoverPreview.style.display = 'none';
  }

  function showToast(msg) {
    var t = ce('div', '__ws-toast', msg);
    document.body.appendChild(t);
    setTimeout(function () { t.style.opacity = '0'; t.style.transition = 'opacity .3s'; }, 1800);
    setTimeout(function () { try { t.remove(); } catch (e) {} }, 2200);
  }

  // ============ 状态管理 ============
  var state = {
    panelOpen: false,
    tab: 'select',
    selectMode: null, // 'container' | 'field'
    containerEl: null,
    containerSelector: '',
    listParent: null,
    listItems: [],
    fields: [],
    extractCache: []
  };

  // ============ 核心选点逻辑 ============
  function startContainerSelect() {
    state.selectMode = 'container';
    state.fields = [];
    state.containerEl = null;
    state.containerSelector = '';
    state.listParent = null;
    state.listItems = [];
    state.tab = 'select';
    ensureStyles();
    if (!state.panelOpen) togglePanel();
    updatePanelUI();
    showToast('将鼠标移到要抓取的<strong>列表区域</strong>上，点击选取（橙色虚线框）');
  }

  function startFieldSelect() {
    if (!state.listItems || !state.listItems.length) return showToast('请先选取列表容器');
    state.selectMode = 'field';
    showToast('现在点击第一个列表项内的<strong>字段</strong>（绿色框），如标题、价格等');
  }

  function cancelSelectMode() {
    state.selectMode = null;
    clearHighlight();
    hideHoverPreview();
    updatePanelUI();
  }

  // ============ 事件处理 ============
  function onMouseMove(e) {
    if (!state.selectMode) return;
    var el = document.elementFromPoint(e.clientX, e.clientY);
    if (!el || el.closest('.__ws-panel') || el.closest('.__ws-toolbar-floating')) { clearHighlight(); hideHoverPreview(); return; }
    if (state.selectMode === 'container') {
      showHighlight(el, '__ws-highlight-container');
      var listInfo = findListItems(el);
      var count = listInfo.items.length;
      var tagInfo = el.tagName.toLowerCase() + (el.classList && el.classList.length ? '.' + el.className.trim().split(/\s+/)[0] : '');
      showHoverPreview('将匹配到 ' + count + ' 个「' + tagInfo + '」\n' + buildContainerSelector(listInfo.parent), e.clientX, e.clientY);
    } else if (state.selectMode === 'field') {
      if (state.listItems.length && state.listItems.indexOf(el) >= 0) {
        // 鼠标在列表项上时追踪其子元素
        showHighlight(el, '__ws-highlight-field');
        var rel = getFieldSelector(el, state.listItems[0]);
        var tagInfo = '';
        if (el.tagName === 'A') tagInfo = ' [链接]';
        else if (el.tagName === 'IMG') tagInfo = ' [图片]';
        var text = (el.textContent || '').replace(/\s+/g, ' ').trim();
        if (text && text.length > 50) text = text.substring(0, 50) + '…';
        showHoverPreview((rel || el.tagName.toLowerCase()) + tagInfo + (text ? '\n' + text : ''), e.clientX, e.clientY);
      } else {
        // 鼠标在某个元素上
        for (var fi = 0; fi < state.listItems.length; fi++) {
          if (state.listItems[fi].contains(el) && el !== state.listItems[fi]) {
            showHighlight(el, '__ws-highlight-field');
            var relEl = getFieldSelector(el, state.listItems[0]);
            var tInfo = '';
            if (el.tagName === 'A') tInfo = ' [链接]';
            else if (el.tagName === 'IMG') tInfo = ' [图片]';
            var txt = (el.textContent || '').replace(/\s+/g, ' ').trim();
            if (txt && txt.length > 50) txt = txt.substring(0, 50) + '…';
            showHoverPreview((relEl || el.tagName.toLowerCase()) + tInfo + (txt ? '\n' + txt : ''), e.clientX, e.clientY);
            return;
          }
        }
        clearHighlight();
        hideHoverPreview();
      }
    }
  }

  function onClick(e) {
    if (!state.selectMode) return;
    e.preventDefault();
    e.stopPropagation();
    var el = document.elementFromPoint(e.clientX, e.clientY);
    if (!el || el.closest('.__ws-panel') || el.closest('.__ws-toolbar-floating')) return;

    if (state.selectMode === 'container') {
      selectContainer(el);
    } else if (state.selectMode === 'field') {
      selectField(el);
    }
  }

  function selectContainer(el) {
    var listInfo = findListItems(el);
    state.listParent = listInfo.parent;
    state.listItems = listInfo.items;
    state.containerSelector = listInfo.containerSelector;
    state.containerEl = el;
    state.selectMode = null;
    clearHighlight();
    hideHoverPreview();

    // 自动识别第一个列表项内可能的字段
    var autoFields = autoDetectFields(el);
    state.fields = autoFields;

    updatePanelUI();
    showToast('已选取 ' + listInfo.items.length + ' 个同类项，自动识别 ' + autoFields.length + ' 个字段');
    extractAll();
  }

  function selectField(el) {
    if (!state.listItems || !state.listItems.length) return;
    // 确认 el 在某个列表项内
    var itemEl = null;
    for (var j = 0; j < state.listItems.length; j++) {
      if (state.listItems[j] === el || state.listItems[j].contains(el)) {
        itemEl = state.listItems[j];
        break;
      }
    }
    if (!itemEl) return showToast('请点击列表项内的字段');
    if (el === itemEl) return;

    var representativeItem = state.listItems[0];
    var fieldSel = getFieldSelector(el, representativeItem);
    if (!fieldSel) return;

    // 验证选择器在至少 2 个列表项里都匹配到
    var validCount = 0;
    for (var v = 0; v < state.listItems.length; v++) {
      try { if (state.listItems[v].querySelector(fieldSel)) validCount++; } catch (e) {}
    }
    if (validCount < 2) {
      showToast('该字段只在那一条里存在，请选一个每条都有的位置');
      return;
    }

    // 检查是否重复
    for (var i = 0; i < state.fields.length; i++) {
      if (state.fields[i].selector === fieldSel) {
        state.fields.splice(i, 1);
        updatePanelUI();
        extractAll();
        return;
      }
    }
    var type = detectFieldType(el);
    var name = genFieldName(el, type);
    state.fields.push({ name: name, selector: fieldSel, type: type });
    updatePanelUI();
    extractAll();
  }

  function autoDetectFields(container) {
    var fields = [];
    var used = {};
    var representativeItem = state.listItems && state.listItems.length ? state.listItems[0] : container;
    var candidates = representativeItem.querySelectorAll('a[href], img, h1, h2, h3, h4, [class*="title"], [class*="name"], [class*="price"], [class*="desc"], span, p, time');
    for (var i = 0; i < candidates.length && fields.length < 6; i++) {
      var el = candidates[i];
      var fieldSel = getFieldSelector(el, representativeItem);
      if (!fieldSel || used[fieldSel]) continue;
      used[fieldSel] = true;
      // 验证至少 2 个列表项里有这个字段
      var cnt = 0;
      for (var j = 0; j < state.listItems.length; j++) {
        try { if (state.listItems[j].querySelector(fieldSel)) cnt++; } catch (e) {}
      }
      if (cnt < 2) continue;
      var type = detectFieldType(el);
      fields.push({ name: genFieldName(el, type), selector: fieldSel, type: type });
    }
    return fields;
  }

  function detectFieldType(el) {
    if (el.tagName === 'A') return 'href';
    if (el.tagName === 'IMG') return 'src';
    var text = (el.textContent || '').replace(/\s+/g, ' ').trim();
    if (/^\d+[\.,]?\d*\s*[¥$€£]?/.test(text) && /[¥$€£]|\d\d/.test(text)) return 'text';
    if (text.length > 40) return 'text';
    return 'text';
  }

  function genFieldName(el, type) {
    var cls = (el.className || '').trim().split(/\s+/).filter(function (c) { return c && c.length < 30 && !/^[0-9]/.test(c); })[0] || '';
    if (cls) {
      var m = cls.match(/(?:title|name|price|desc|content|text|link|image|src|href|time|date|author)/i);
      if (m) return m[1].toLowerCase();
    }
    var typeNames = { href: '链接', src: '图片', text: '文本', html: 'HTML', attr: '属性' };
    return typeNames[type] || '字段';
  }

  // ============ 数据提取 ============
  function extractAll() {
    if (!state.containerEl || !state.similarSelector) return;
    var containerSel = state.similarSelector;
    try {
      var items = document.querySelectorAll(containerSel);
      var results = [];
      for (var i = 0; i < items.length; i++) {
        var row = extractRow(items[i]);
        if (hasContent(row)) results.push(row);
      }
      state.extractCache = results;
      if (state.tab === 'preview') renderPreview();
    } catch (e) {
      console.warn('[web-scraper] 提取失败:', e);
    }
  }

  function extractRow(item) {
    var row = {};
    for (var i = 0; i < state.fields.length; i++) {
      var f = state.fields[i];
      var target = null;
      try {
        target = f.selector ? item.querySelector(f.selector) : item;
      } catch (e) {}
      row[f.name + (f.suffix || '')] = getFieldValue(target, f);
    }
    return row;
  }

  function getFieldValue(node, f) {
    if (!node) return '';
    switch (f.type) {
      case 'href':
        var a = node.tagName === 'A' ? node : node.querySelector('a[href]');
        return a ? (a.href || a.getAttribute('href') || '') : '';
      case 'src':
        var img = node.tagName === 'IMG' ? node : node.querySelector('img[src]');
        return img ? (img.src || img.getAttribute('src') || '') : '';
      case 'html': return node.innerHTML || '';
      default: return (node.textContent || '').replace(/\s+/g, ' ').trim();
    }
  }

  function hasContent(row) {
    var keys = Object.keys(row);
    for (var i = 0; i < keys.length; i++) {
      if (String(row[keys[i]] || '').trim().length > 0) return true;
    }
    return false;
  }

  // ============ 导出 ============
  function exportCSV() {
    if (!state.extractCache.length) return showToast('没有可导出的数据');
    var keys = [];
    state.extractCache.forEach(function (row) { Object.keys(row).forEach(function (k) { if (keys.indexOf(k) === -1) keys.push(k); }); });
    var csv = '\uFEFF' + keys.join(',') + '\n' + state.extractCache.map(function (row) {
      return keys.map(function (k) {
        var v = String(row[k] || '').replace(/"/g, '""').replace(/\n/g, ' ');
        return v.indexOf(',') >= 0 || v.indexOf('"') >= 0 ? '"' + v + '"' : v;
      }).join(',');
    }).join('\n');
    download(csv, 'scrape-export.csv', 'text/csv;charset=utf-8');
    showToast('已导出 CSV（' + state.extractCache.length + ' 条）');
  }

  function exportJSON() {
    if (!state.extractCache.length) return showToast('没有可导出的数据');
    download(JSON.stringify(state.extractCache, null, 2), 'scrape-export.json', 'application/json');
    showToast('已导出 JSON（' + state.extractCache.length + ' 条）');
  }

  function download(content, filename, mime) {
    var blob = new Blob([content], { type: mime });
    var url = URL.createObjectURL(blob);
    var a = ce('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    setTimeout(function () { URL.revokeObjectURL(url); a.remove(); }, 100);
  }

  // ============ UI 构建 ============
  var panelEl = null;
  var fabEl = null;

  function createPanel() {
    panelEl = ce('div', '__ws-panel __ws-panel-hidden');
    panelEl.innerHTML =
      '<div class="__ws-panel-header">' +
        '<span>爬 虫 工 具</span>' +
        '<button class="__ws-panel-close" title="关闭">×</button>' +
      '</div>' +
      '<div class="__ws-panel-tabs">' +
        '<div class="__ws-tab __ws-tab-active" data-tab="select">选点</div>' +
        '<div class="__ws-tab" data-tab="fields">字段</div>' +
        '<div class="__ws-tab" data-tab="preview">预览</div>' +
      '</div>' +
      '<div class="__ws-panel-body"></div>' +
      '<div class="__ws-panel-footer">' +
        '<button class="__ws-btn __ws-btn-primary reextract">重新提取</button>' +
        '<button class="__ws-btn __ws-btn-success export-csv">导出CSV</button>' +
        '<button class="__ws-btn export-json">导出JSON</button>' +
      '</div>';
    document.body.appendChild(panelEl);

    // Tab 切换
    var tabs = panelEl.querySelectorAll('.__ws-tab');
    tabs.forEach(function (tab) {
      tab.addEventListener('click', function () {
        state.tab = tab.getAttribute('data-tab');
        tabs.forEach(function (t) { t.classList.toggle('__ws-tab-active', t === tab); });
        updatePanelUI();
      });
    });

    // 关闭按钮
    $('.__ws-panel-close', panelEl).addEventListener('click', togglePanel);
    // 底部按钮
    $(' .reextract', panelEl).addEventListener('click', function () { extractAll(); updatePanelUI(); showToast('已重新提取 ' + state.extractCache.length + ' 条数据'); });
    $(' .export-csv', panelEl).addEventListener('click', exportCSV);
    $(' .export-json', panelEl).addEventListener('click', exportJSON);

    // 事件绑定
    document.addEventListener('mousemove', onMouseMove, true);
    document.addEventListener('click', onClick, true);
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') { if (state.selectMode) cancelSelectMode(); }
    });
  }

  function togglePanel() {
    if (!panelEl) createPanel();
    state.panelOpen = !state.panelOpen;
    panelEl.classList.toggle('__ws-panel-hidden', !state.panelOpen);
    if (state.panelOpen && state.tab) updatePanelUI();
  }

  function updatePanelUI() {
    if (!panelEl) return;
    var body = $('.__ws-panel-body', panelEl);
    if (state.tab === 'select') renderSelectTab(body);
    else if (state.tab === 'fields') renderFieldsTab(body);
    else if (state.tab === 'preview') renderPreviewTab(body);

    // 更新 tab active 状态
    var tabs = panelEl.querySelectorAll('.__ws-tab');
    tabs.forEach(function (t) { t.classList.toggle('__ws-tab-active', t.getAttribute('data-tab') === state.tab); });
  }

  function renderSelectTab(body) {
    var html =
      '<div class="__ws-steps">' +
        '<div class="__ws-step ' + (state.containerEl ? '__ws-step-done' : (state.selectMode === 'container' ? '__ws-step-active' : '')) + '">① 选容器</div>' +
        '<span class="__ws-step-arrow">›</span>' +
        '<div class="__ws-step ' + (state.fields.length ? '__ws-step-done' : (state.selectMode === 'field' ? '__ws-step-active' : '')) + '">② 选字段</div>' +
        '<span class="__ws-step-arrow">›</span>' +
        '<div class="__ws-step ' + (state.extractCache.length ? '__ws-step-done' : '') + '">③ 预览</div>' +
      '</div>';

    if (!state.containerEl && !state.selectMode) {
      html += '<div class="__ws-empty"><div class="__ws-empty-icon">🖱️</div><div><strong>步骤 1：选择列表容器</strong></div><div style="margin-top:8px">点击下方按钮，然后把鼠标移到你想要抓取的<br><strong>整块列表区域</strong>上，点击选取。</div></div>' +
        '<button class="__ws-btn __ws-btn-primary __ws-btn-active start-container" style="width:100%;margin-top:12px;padding:12px;font-size:14px">在此页面启用选点</button>';
    } else if (state.selectMode === 'container') {
      html += '<div style="text-align:center;padding:20px"><div style="font-size:32px;margin-bottom:12px">🖱️</div><div style="color:#e65100;font-weight:600">正在选取容器…</div><div style="margin-top:8px;color:#666">鼠标移到列表块上会显示橙色虚框<br>点击选取后自动识别相似项</div>' +
        '<button class="__ws-btn cancel-select" style="margin-top:16px">取消</button></div>';
    } else if (state.containerEl) {
      html += '<div style="padding:10px;background:#fff3e0;border-radius:6px;margin-bottom:12px">' +
        '<div><span class="__ws-badge __ws-badge-info">已选取</span> <strong>' + state.similarCount + '</strong> 个相似项</div>' +
        '<div style="margin-top:6px;font-family:monospace;font-size:11px;color:#666;word-break:break-all">' + escapeHtml(state.similarSelector) + '</div>' +
        '</div>' +
        '<button class="__ws-btn add-field" style="width:100%;margin-bottom:8px;' + (state.selectMode === 'field' ? '__ws-btn-active' : '') + '">' + (state.selectMode === 'field' ? '🖱️ 正在选取字段…（按 Esc 取消）' : '+ 添加字段（点击页面元素）') + '</button>' +
        '<div style="display:flex;gap:6px;margin-top:6px">' +
          '<button class="__ws-btn reselect-container" style="flex:1">重新选容器</button>' +
          '<button class="__ws-btn goto-fields" style="flex:1">管理字段(' + state.fields.length + ')</button>' +
          '<button class="__ws-btn goto-preview" style="flex:1">预览(' + state.extractCache.length + ')</button>' +
        '</div>';
    }

    body.innerHTML = html;

    var btn = $(' .start-container', body);
    if (btn) btn.addEventListener('click', startContainerSelect);
    var cBtn = $(' .cancel-select', body);
    if (cBtn) cBtn.addEventListener('click', cancelSelectMode);
    var rBtn = $(' .reselect-container', body);
    if (rBtn) rBtn.addEventListener('click', startContainerSelect);
    var aBtn = $(' .add-field', body);
    if (aBtn) aBtn.addEventListener('click', function () { if (state.selectMode === 'field') cancelSelectMode(); else startFieldSelect(); });
    var gfBtn = $(' .goto-fields', body);
    if (gfBtn) gfBtn.addEventListener('click', function () { state.tab = 'fields'; updatePanelUI(); });
    var gpBtn = $(' .goto-preview', body);
    if (gpBtn) gpBtn.addEventListener('click', function () { state.tab = 'preview'; updatePanelUI(); });
  }

  function renderFieldsTab(body) {
    if (!state.containerEl) {
      body.innerHTML = '<div class="__ws-empty"><div class="__ws-empty-icon">📋</div><div>请先完成步骤①选取容器</div></div>';
      return;
    }
    if (!state.fields.length) {
      body.innerHTML = '<div class="__ws-empty"><div class="__ws-empty-icon">📝</div><div>暂无字段，点击下方按钮<br>然后点击容器内的元素添加</div>' +
        '<button class="__ws-btn __ws-btn-primary add-field-first" style="width:100%;margin-top:12px">添加字段</button></div>';
      $('.add-field-first', body).addEventListener('click', startFieldSelect);
      return;
    }
    var html = '<div style="margin-bottom:10px"><strong>已定义字段</strong> <span style="color:#999">(' + state.fields.length + ')</span> ' +
      '<button class="__ws-btn add-more" style="float:right;padding:4px 10px">+ 添加</button></div>';
    state.fields.forEach(function (f, i) {
      var previewText = getFieldPreview(f);
      html += '<div class="__ws-field-card" data-idx="' + i + '">' +
        '<div class="__ws-field-label">' + escapeHtml(f.name) + ' <span style="color:#999;font-weight:400;text-transform:uppercase">' + f.type + '</span></div>' +
        '<div class="__ws-field-sel">' + escapeHtml(f.selector) + '</div>' +
        '<div class="__ws-field-preview">' + escapeHtml(previewText) + '</div>' +
        '<div style="margin-top:6px;text-align:right"><button class="__ws-btn del-field" data-idx="' + i + '" style="padding:2px 8px;font-size:11px;color:#e53935">删除</button></div>' +
        '</div>';
    });
    body.innerHTML = html;
    $('.add-more', body).addEventListener('click', startFieldSelect);
    body.querySelectorAll('.del-field').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var idx = parseInt(btn.getAttribute('data-idx'));
        state.fields.splice(idx, 1);
        updatePanelUI();
        extractAll();
      });
    });
  }

  function getFieldPreview(f) {
    if (!state.extractCache || !state.extractCache.length) return '(无数据)';
    var sample = state.extractCache.slice(0, 3).map(function (row) { return String(row[f.name] || '(空)'); }).join(' | ');
    return sample || '(空)';
  }

  function renderPreviewTab(body) {
    if (!state.extractCache.length) {
      body.innerHTML = '<div class="__ws-empty"><div class="__ws-empty-icon">📊</div><div>暂无数据，请完成选点</div></div>';
      return;
    }
    var keys = Object.keys(state.extractCache[0]);
    var html = '<div style="margin-bottom:10px"><strong>数据预览</strong> <span style="color:#999">(' + state.extractCache.length + ' 条)</span> ' +
      '<button class="__ws-btn refresh-preview" style="float:right;padding:4px 10px">刷新</button></div>' +
      '<div style="overflow-x:auto;max-height:calc(100vh - 220px);overflow-y:auto">' +
      '<table class="__ws-table"><thead><tr><th>#</th>';
    keys.forEach(function (k) { html += '<th>' + escapeHtml(k) + '</th>'; });
    html += '</tr></thead><tbody>';
    var limit = Math.min(state.extractCache.length, 100);
    for (var i = 0; i < limit; i++) {
      html += '<tr><td style="color:#999">' + (i + 1) + '</td>';
      keys.forEach(function (k) { html += '<td title="' + escapeHtml(String(state.extractCache[i][k] || '')) + '">' + escapeHtml(String(state.extractCache[i] || '')) + '</td>'; });
      html += '</tr>';
    }
    html += '</tbody></table></div>';
    if (state.extractCache.length > 100) html += '<div style="padding:8px;color:#999;text-align:center">仅显示前 100 条，共 ' + state.extractCache.length + ' 条</div>';
    body.innerHTML = html;
    $('.refresh-preview', body).addEventListener('click', function () { extractAll(); showToast('已刷新 ' + state.extractCache.length + ' 条数据'); });
  }

  function escapeHtml(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  function renderPreview() { updatePanelUI(); }

  // ============ FAB 悬浮按钮 ============
  function createFAB() {
    fabEl = ce('div', '__ws-toolbar-floating');
    fabEl.innerHTML = '<button class="__ws-fab" title="爬虫工具 (Ctrl+Alt+G)">🔍</button>';
    document.body.appendChild(fabEl);
    $('. __ws-fab', fabEl).addEventListener('click', togglePanel);
  }

  // ============ 初始化 ============
  ensureStyles();
  createPanel();
  createFAB();

  // 快捷键
  document.addEventListener('keydown', function (e) {
    if (e.ctrlKey && e.altKey && (e.key === 'g' || e.key === 'G')) {
      e.preventDefault();
      togglePanel();
    }
  });

  console.log('[web-scraper] 可视化爬虫就绪，按 Ctrl+Alt+G 或点击右下角按钮打开面板');
})();
