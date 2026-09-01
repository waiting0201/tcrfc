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
