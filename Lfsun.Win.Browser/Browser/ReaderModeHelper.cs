namespace Lfsun.Win.Browser.Browser;

/// <summary>
/// 中文注释：沉浸式阅读模式与视频画中画辅助脚本管理器。
/// </summary>
public static class ReaderModeHelper
{
    public const string ExitReaderScript = "window.__lfsun_exit_reader && window.__lfsun_exit_reader();";

    public const string ToggleReaderScript = """
(() => {
    try {
        if (document.getElementById('__lfsun_reader_overlay__')) {
            if (window.__lfsun_exit_reader) window.__lfsun_exit_reader();
            return JSON.stringify({ active: false, ok: true });
        }

        const title = (document.querySelector('h1')?.innerText || document.title || '文章阅读').trim();
        const selectors = [
            'article', 'main', '.post-content', '.article-content', '.entry-content',
            '.markdown-body', '#article-content', '#content', '.content', '.article'
        ];
        let contentEl = null;
        for (const sel of selectors) {
            const found = document.querySelector(sel);
            if (found && found.innerText.trim().length > 150) {
                contentEl = found;
                break;
            }
        }

        if (!contentEl) {
            const blocks = Array.from(document.querySelectorAll('div, section'));
            let best = null, maxLen = 0;
            for (const b of blocks) {
                const pCount = b.querySelectorAll('p').length;
                const len = b.innerText.trim().length;
                if (pCount >= 2 && len > maxLen && len < 200000) {
                    maxLen = len;
                    best = b;
                }
            }
            contentEl = best || document.body;
        }

        const clone = contentEl.cloneNode(true);
        const toRemove = clone.querySelectorAll('script, style, iframe, noscript, svg, nav, footer, header, form, button, input, .ad, .ads, .advertisement');
        toRemove.forEach(n => n.remove());

        const text = clone.innerText || '';
        const wordCount = text.length;
        const estMinutes = Math.max(1, Math.ceil(wordCount / 350));

        const style = document.createElement('style');
        style.id = '__lfsun_reader_style__';
        style.textContent = `
            #__lfsun_reader_overlay__ {
                position: fixed !important;
                inset: 0 !important;
                z-index: 2147483646 !important;
                background: var(--rm-bg, #fcfcfc) !important;
                color: var(--rm-fg, #24292f) !important;
                overflow-y: auto !important;
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", serif !important;
                padding: 0 0 80px 0 !important;
                transition: background-color 0.2s, color 0.2s !important;
            }
            #__lfsun_reader_overlay__.theme-light {
                --rm-bg: #fcfcfc; --rm-fg: #24292f; --rm-bar-bg: #f0f2f5; --rm-border: #e1e4e8; --rm-btn-bg: #ffffff;
            }
            #__lfsun_reader_overlay__.theme-sepia {
                --rm-bg: #f4ecd8; --rm-fg: #5b4636; --rm-bar-bg: #e8dcbf; --rm-border: #dcd0b3; --rm-btn-bg: #faefe0;
            }
            #__lfsun_reader_overlay__.theme-dark {
                --rm-bg: #1a1b1e; --rm-fg: #d0d2d6; --rm-bar-bg: #25262b; --rm-border: #373a40; --rm-btn-bg: #2c2e33;
            }
            .rm-topbar {
                position: sticky; top: 0; z-index: 10;
                display: flex; align-items: center; justify-content: space-between;
                padding: 10px 24px; background: var(--rm-bar-bg);
                border-bottom: 1px solid var(--rm-border);
                box-shadow: 0 2px 10px rgba(0,0,0,0.06);
            }
            .rm-stats { font-size: 13px; opacity: 0.85; font-weight: 500; }
            .rm-actions { display: flex; align-items: center; gap: 8px; }
            .rm-btn {
                background: var(--rm-btn-bg); color: inherit; border: 1px solid var(--rm-border);
                padding: 5px 12px; border-radius: 6px; cursor: pointer; font-size: 13px; font-weight: 500;
                transition: all 0.15s ease;
            }
            .rm-btn:hover { filter: brightness(0.92); transform: translateY(-1px); }
            .rm-container {
                max-width: var(--rm-max-width, 780px); margin: 40px auto 0 auto; padding: 0 24px;
                font-size: var(--rm-font-size, 17px); line-height: 1.85; word-wrap: break-word;
            }
            .rm-title { font-size: 28px; font-weight: 700; line-height: 1.35; margin-bottom: 24px; }
            .rm-content p { margin: 1.2em 0; }
            .rm-content img { max-width: 100%; height: auto; border-radius: 8px; margin: 16px 0; display: block; }
            .rm-content pre, .rm-content code { background: rgba(125,125,125,0.12); border-radius: 4px; font-family: Consolas, monospace; }
            .rm-content pre { padding: 12px; overflow-x: auto; }
            .rm-content code { padding: 2px 5px; }
            .rm-content blockquote { border-left: 4px solid #3b82f6; margin: 1.2em 0; padding-left: 16px; opacity: 0.85; }
        `;
        document.head.appendChild(style);

        const overlay = document.createElement('div');
        overlay.id = '__lfsun_reader_overlay__';
        overlay.className = 'theme-light';

        const topbar = document.createElement('div');
        topbar.className = 'rm-topbar';
        topbar.innerHTML = `
            <div class='rm-stats'>📖 沉浸阅读模式 · 预计阅读 ${estMinutes} 分钟 (${wordCount} 字)</div>
            <div class='rm-actions'>
                <button class='rm-btn' id='__rm_btn_light'>☀️ 白天</button>
                <button class='rm-btn' id='__rm_btn_sepia'>📜 暖黄</button>
                <button class='rm-btn' id='__rm_btn_dark'>🌙 夜间</button>
                <button class='rm-btn' id='__rm_btn_fminus'>A-</button>
                <button class='rm-btn' id='__rm_btn_fplus'>A+</button>
                <button class='rm-btn' id='__rm_btn_width'>↔ 宽度</button>
                <button class='rm-btn' id='__rm_btn_exit' style='background:#ef4444;color:#ffffff;border-color:#ef4444;'>✕ 退出 (Esc)</button>
            </div>
        `;
        overlay.appendChild(topbar);

        const container = document.createElement('div');
        container.className = 'rm-container';

        const h1 = document.createElement('h1');
        h1.className = 'rm-title';
        h1.textContent = title;
        container.appendChild(h1);

        const content = document.createElement('div');
        content.className = 'rm-content';
        content.innerHTML = clone.innerHTML;
        container.appendChild(content);

        overlay.appendChild(container);
        document.body.appendChild(overlay);

        let currentSize = 17;
        let isWide = false;

        document.getElementById('__rm_btn_light').onclick = () => { overlay.className = 'theme-light'; };
        document.getElementById('__rm_btn_sepia').onclick = () => { overlay.className = 'theme-sepia'; };
        document.getElementById('__rm_btn_dark').onclick = () => { overlay.className = 'theme-dark'; };
        document.getElementById('__rm_btn_fminus').onclick = () => {
            currentSize = Math.max(13, currentSize - 2);
            container.style.setProperty('--rm-font-size', currentSize + 'px');
        };
        document.getElementById('__rm_btn_fplus').onclick = () => {
            currentSize = Math.min(26, currentSize + 2);
            container.style.setProperty('--rm-font-size', currentSize + 'px');
        };
        document.getElementById('__rm_btn_width').onclick = () => {
            isWide = !isWide;
            container.style.setProperty('--rm-max-width', isWide ? '1060px' : '780px');
        };

        const exitReader = () => {
            overlay.remove();
            style.remove();
            window.removeEventListener('keydown', onKey);
            window.__lfsun_exit_reader = null;
        };

        const onKey = (e) => {
            if (e.key === 'Escape') {
                exitReader();
            }
        };

        window.addEventListener('keydown', onKey);
        window.__lfsun_exit_reader = exitReader;
        document.getElementById('__rm_btn_exit').onclick = exitReader;

        return JSON.stringify({ active: true, ok: true });
    } catch (err) {
        return JSON.stringify({ active: false, ok: false, error: err.message });
    }
})()
""";

    public const string PictureInPictureScript = """
(() => {
    try {
        const v = document.querySelector('video');
        if (!v) {
            return JSON.stringify({ ok: false, active: false, message: '当前页面未检测到视频元素' });
        }
        if (document.pictureInPictureElement) {
            document.exitPictureInPicture();
            return JSON.stringify({ ok: true, active: false, message: '已退出视频画中画浮窗' });
        } else {
            v.requestPictureInPicture();
            return JSON.stringify({ ok: true, active: true, message: '已开启视频画中画独立浮窗 📺' });
        }
    } catch (err) {
        return JSON.stringify({ ok: false, active: false, message: '画中画触发异常: ' + err.message });
    }
})()
""";
}
