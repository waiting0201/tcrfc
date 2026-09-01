(function(){
  "use strict";
  // Sticky header shadow
  var header = document.getElementById('site-header');
  var onScroll = function(){
    if (window.scrollY > 8) header.classList.add('is-scrolled');
    else header.classList.remove('is-scrolled');
  };
  document.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  // Mobile nav toggle
  var openBtn = document.getElementById('menu-open-btn');
  var closeBtn = document.getElementById('menu-close-btn');
  var nav = document.getElementById('mobile-nav');
  function openNav(){
    nav.classList.add('is-open');
    openBtn.setAttribute('aria-expanded', 'true');
    document.body.style.overflow = 'hidden';
    closeBtn.focus();
  }
  function closeNav(){
    nav.classList.remove('is-open');
    openBtn.setAttribute('aria-expanded', 'false');
    document.body.style.overflow = '';
    openBtn.focus();
  }
  openBtn.addEventListener('click', openNav);
  closeBtn.addEventListener('click', closeNav);
  nav.addEventListener('click', function(e){
    if (e.target.tagName === 'A') closeNav();
  });
  document.addEventListener('keydown', function(e){
    if (e.key === 'Escape' && nav.classList.contains('is-open')) closeNav();
  });

  // Team chips (match band)
  var chips = document.querySelectorAll('.team-chip');
  var panelD1 = document.getElementById('match-grid-d1');
  var panelOther = document.getElementById('match-grid-other');
  chips.forEach(function(chip){
    chip.addEventListener('click', function(){
      chips.forEach(function(c){ c.setAttribute('aria-pressed', 'false'); });
      chip.setAttribute('aria-pressed', 'true');
      var isD1 = chip.dataset.team === 'D1';
      panelD1.hidden = !isD1;
      panelOther.hidden = isD1;
    });
  });
})();

/* Mega Menu — hover 與鍵盤皆可開啟，Esc 關閉
   .mega 是 position:absolute; top:100%，定位基準是 .site-header（.main-nav li
   刻意設為 position:static，讓面板能相對整個 header 全寬展開）。這代表 <li>
   的版面高度只等於連結本身，連結底緣與 .mega 頂緣之間，隔著 header 置中對齊
   留下的一段「死區」——游標往下移動經過這段死區時會先離開 <li> 的命中範圍，
   觸發 mouseleave，選單才還沒到就關閉了。
   兩段式修法：
   1) 關閉延遲（CLOSE_DELAY）：mouseleave 先不關，等一小段時間；只要游標在
      期限內抵達 .mega（.mega 在 DOM 上仍是 <li> 的子節點，進入它會讓 <li>
      重新收到 mouseenter，取消關閉），選單就不會消失。這個機制本身也涵蓋了
      多數斜向移動的情況，因為 .mega 面板寬度幾乎與 header 同寬，游標只要
      在延遲時間內落入面板範圍就算數，不需要精準對準路徑。
   2) CSS 死區橋接（.has-mega > a::before，見 tcrfc.css）：在觸發連結正下方
      補一塊不可見的可命中區域，讓 hover 範圍實際上連續，delay 只是保險。 */
(function(){
  "use strict";
  var items = document.querySelectorAll('.main-nav .has-mega');
  var openItem = null;
  var closeTimer = null;
  var CLOSE_DELAY = 220; // ms — 游標跨越死區所需的緩衝時間

  function cancelClose(){
    if(closeTimer){ clearTimeout(closeTimer); closeTimer = null; }
  }
  function hide(li){
    if(!li) return;
    var m = li.querySelector('.mega');
    if(m) m.hidden = true;
    li.classList.remove('is-open');
    if(openItem === li) openItem = null;
  }
  function open(li){
    cancelClose();
    if(openItem && openItem !== li) hide(openItem);
    var m = li.querySelector('.mega');
    if(m) m.hidden = false;
    li.classList.add('is-open');
    openItem = li;
  }
  function scheduleClose(li){
    cancelClose();
    closeTimer = setTimeout(function(){
      closeTimer = null;
      hide(li);
    }, CLOSE_DELAY);
  }

  items.forEach(function(li){
    li.addEventListener('mouseenter', function(){ open(li); });
    li.addEventListener('mouseleave', function(){ scheduleClose(li); });
    li.addEventListener('focusin', function(){ open(li); });
    li.addEventListener('focusout', function(e){
      if(!li.contains(e.relatedTarget)) hide(li);
    });
  });
  document.addEventListener('keydown', function(e){
    if(e.key === 'Escape' && openItem){ cancelClose(); hide(openItem); }
  });
})();

/* Hero slider — "方塊拆解" (block-dismantle) transition.
   The outgoing slide's photo is temporarily covered by a grid of tiles (built
   fresh for each transition, then discarded) that each show a crop of that same
   photo via background-position — one shared image URL, no per-tile network
   request. The tiles' background-size/position are computed from the outgoing
   <img>'s real object-fit:cover geometry (container size, natural size, and its
   object-position), so the mosaic lines up pixel-for-pixel with the photo
   beneath it at any viewport width. The tiles then fade + shrink outward from
   the grid's centre with a short stagger, revealing the incoming slide (already
   swapped in underneath) — restrained, ~0.9s, no rotation or colour shift. */
(function(){
  "use strict";
  var heroSection = document.getElementById('top');
  var slider = document.getElementById('hero-slider');
  if(!heroSection || !slider) return;

  var slides = Array.prototype.slice.call(slider.querySelectorAll('.hero__slide'));
  var total = slides.length;
  if(total < 2) return;

  var statusEl = document.getElementById('hero-slide-status');
  var prevBtn = heroSection.querySelector('[data-hero-prev]');
  var nextBtn = heroSection.querySelector('[data-hero-next]');
  var dots = Array.prototype.slice.call(heroSection.querySelectorAll('[data-hero-goto]'));

  var COLS = 6, ROWS = 4;
  var TILE_DURATION = 520;   // ms — matches the CSS transition on .hero__tile
  var STAGGER_SPAN = 420;    // ms — spread of transition-delay across the grid
  var AUTOPLAY_MS = 6500;

  var current = 0;
  var isAnimating = false;
  var timer = null;
  var reduceMQ = window.matchMedia('(prefers-reduced-motion: reduce)');

  function reduced(){ return reduceMQ.matches; }

  function updateControls(){
    dots.forEach(function(dot, i){
      var active = i === current;
      dot.classList.toggle('is-active', active);
      dot.setAttribute('aria-selected', active ? 'true' : 'false');
    });
    if(statusEl) statusEl.textContent = '目前顯示第 ' + (current + 1) + ' 張，共 ' + total + ' 張';
  }

  function swapInstant(index){
    slides[current].classList.remove('is-active');
    slides[current].setAttribute('aria-hidden', 'true');
    slides[index].classList.add('is-active');
    slides[index].removeAttribute('aria-hidden');
    current = index;
    updateControls();
  }

  // Builds the tile overlay from the outgoing slide's <img>, matching its
  // computed object-fit:cover box exactly, then animates the tiles away.
  function animateSwap(index){
    var outgoing = slides[current];
    var incoming = slides[index];
    var img = outgoing.querySelector('img');
    if(!img || !img.naturalWidth){
      // Outgoing slide isn't actually decoded yet (shouldn't happen — it's the
      // one currently on screen) — fall back to an instant cut rather than
      // building a mosaic from geometry we can't trust.
      swapInstant(index);
      return;
    }
    isAnimating = true;

    var rect = slider.getBoundingClientRect();
    var cw = rect.width, ch = rect.height;
    var naturalW = img.naturalWidth, naturalH = img.naturalHeight;
    var imgAspect = naturalW / naturalH, boxAspect = cw / ch;
    var scaledW, scaledH;
    if(boxAspect > imgAspect){ scaledW = cw; scaledH = cw / imgAspect; }
    else { scaledH = ch; scaledW = ch * imgAspect; }

    var posParts = getComputedStyle(img).objectPosition.split(' ');
    var posX = (parseFloat(posParts[0]) || 50) / 100;
    var posY = (parseFloat(posParts[1]) || 50) / 100;
    var offsetX = (cw - scaledW) * posX;
    var offsetY = (ch - scaledH) * posY;
    var srcUrl = img.currentSrc || img.src;

    // The tiles above show crops of the OUTGOING photo, so it's the incoming
    // slide that must be lifted above the (still fully-opaque) outgoing slide —
    // otherwise fading tiles would just reveal more of the same outgoing image
    // sitting right behind them, and the swap would look like nothing happens
    // until an abrupt cut at the very end.
    incoming.classList.add('is-active', 'hero__slide--front');
    incoming.removeAttribute('aria-hidden');

    var tiles = document.createElement('div');
    tiles.className = 'hero__tiles';
    tiles.style.setProperty('--tile-cols', COLS);
    tiles.style.setProperty('--tile-rows', ROWS);

    var tileW = cw / COLS, tileH = ch / ROWS;
    var cx = (COLS - 1) / 2, cy = (ROWS - 1) / 2;
    var maxDist = Math.sqrt(cx * cx + cy * cy) || 1;
    var frag = document.createDocumentFragment();

    for(var r = 0; r < ROWS; r++){
      for(var c = 0; c < COLS; c++){
        var tile = document.createElement('span');
        tile.className = 'hero__tile';
        tile.style.backgroundImage = 'url("' + srcUrl + '")';
        tile.style.backgroundSize = scaledW.toFixed(1) + 'px ' + scaledH.toFixed(1) + 'px';
        tile.style.backgroundPosition = (offsetX - c * tileW).toFixed(1) + 'px ' + (offsetY - r * tileH).toFixed(1) + 'px';

        var dx = c - cx, dy = r - cy;
        var dist = Math.sqrt(dx * dx + dy * dy);
        var len = dist || 1;
        tile.style.transitionDelay = ((dist / maxDist) * STAGGER_SPAN).toFixed(0) + 'ms';
        tile.style.setProperty('--tx', ((dx / len) * 12).toFixed(1) + 'px');
        tile.style.setProperty('--ty', ((dy / len) * 12).toFixed(1) + 'px');
        frag.appendChild(tile);
      }
    }
    tiles.appendChild(frag);
    slider.appendChild(tiles);

    // Force layout so the initial (fully-assembled) state paints before the
    // transition-triggering class is added on the next frame.
    void tiles.offsetWidth;
    window.requestAnimationFrame(function(){
      tiles.classList.add('is-out');
    });

    window.setTimeout(function(){
      outgoing.classList.remove('is-active');
      outgoing.setAttribute('aria-hidden', 'true');
      incoming.classList.remove('hero__slide--front');
      slider.removeChild(tiles);
      current = index;
      isAnimating = false;
      updateControls();
    }, STAGGER_SPAN + TILE_DURATION + 60);
  }

  function goTo(index){
    index = ((index % total) + total) % total;
    if(index === current || isAnimating) return;
    if(reduced()) swapInstant(index);
    else animateSwap(index);
  }

  function startAutoplay(){
    if(reduced() || total < 2) return;
    stopAutoplay();
    timer = window.setInterval(function(){ goTo(current + 1); }, AUTOPLAY_MS);
    if(statusEl) statusEl.setAttribute('aria-live', 'off');
  }
  function stopAutoplay(){
    if(timer){ window.clearInterval(timer); timer = null; }
  }
  function pauseForInteraction(){
    stopAutoplay();
    if(statusEl) statusEl.setAttribute('aria-live', 'polite');
  }
  function resumeAutoplay(){
    startAutoplay();
  }

  if(prevBtn) prevBtn.addEventListener('click', function(){ goTo(current - 1); });
  if(nextBtn) nextBtn.addEventListener('click', function(){ goTo(current + 1); });
  dots.forEach(function(dot){
    dot.addEventListener('click', function(){
      goTo(parseInt(dot.getAttribute('data-hero-goto'), 10));
    });
  });

  // Autoplay is interruptible by hover or keyboard focus anywhere in the hero
  // (matches the mega-menu's hero/focusout pattern above).
  heroSection.addEventListener('mouseenter', pauseForInteraction);
  heroSection.addEventListener('mouseleave', resumeAutoplay);
  heroSection.addEventListener('focusin', pauseForInteraction);
  heroSection.addEventListener('focusout', function(e){
    if(!heroSection.contains(e.relatedTarget)) resumeAutoplay();
  });

  if(reduceMQ.addEventListener){
    reduceMQ.addEventListener('change', function(){
      if(reduced()) stopAutoplay();
      else resumeAutoplay();
    });
  }

  updateControls();
  startAutoplay();
})();
