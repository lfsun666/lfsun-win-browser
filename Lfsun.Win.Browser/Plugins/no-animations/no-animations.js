(function () {
  const styleId = "lfsun-no-animations-style";
  if (document.getElementById(styleId)) {
    return;
  }

  const style = document.createElement("style");
  style.id = styleId;
  style.textContent = `
    *, *::before, *::after {
      animation: none !important;
      transition: none !important;
      scroll-behavior: auto !important;
    }
  `;

  document.documentElement.appendChild(style);
})();

