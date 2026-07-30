// 中文注释：夜间阅读示例内容脚本：整页反色 + 媒体元素二次反转还原本色。
(function () {
    function applyNightReader() {
        if (document.getElementById('lp-night-reader-style')) {
            return;
        }

        var style = document.createElement('style');
        style.id = 'lp-night-reader-style';
        style.textContent =
            'html { filter: invert(1) hue-rotate(180deg) !important; background: #111 !important; }' +
            'img, video, canvas, picture, iframe { filter: invert(1) hue-rotate(180deg) !important; }';
        (document.head || document.documentElement).appendChild(style);
        console.info('[night-reader] 夜间阅读样式已应用');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyNightReader);
    } else {
        applyNightReader();
    }
})();
