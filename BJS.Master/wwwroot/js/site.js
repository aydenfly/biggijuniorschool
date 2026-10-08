try {
    /* burger nav */
    const burger = document.getElementById('menu-toggler'), navpanel = document.getElementById('navpanel');
    burger.addEventListener('click', () => { burger.classList.toggle('open'); navpanel.classList.toggle('open'); });
    navpanel.querySelectorAll('a').forEach(a => a.addEventListener('click', () => { burger.classList.remove('open'); navpanel.classList.remove('open'); }));

    /* header logo appears only after the hero logo scrolls out of view */
    const topbarEl = document.querySelector('.topbar'), heroLogo = document.querySelector('.hero-left .site-logo');
    if (heroLogo && 'IntersectionObserver' in window) {
        new IntersectionObserver(([en]) => {
            topbarEl.classList.toggle('show-logo', !en.isIntersecting);
        }, { rootMargin: '-70px 0px 0px 0px' }).observe(heroLogo);
    } else { topbarEl.classList.add('show-logo'); }

    /* real-path nav links, processed as in-page scrolls via JS */
    function scrollToSection(target) {
        const offset = topbarEl.getBoundingClientRect().height;
        const y = target.getBoundingClientRect().top + window.scrollY - offset;
        window.scrollTo({ top: y, behavior: 'smooth' });
    }
    document.querySelectorAll('a[data-target]').forEach(a => {
        a.addEventListener('click', (e) => {
            const target = document.getElementById(a.dataset.target);
            if (!target) return;
            e.preventDefault();
            //target.scrollIntoView({ behavior: 'smooth' });
            scrollToSection(target);
            if (window.history && window.history.pushState) { window.history.pushState(null, '', a.getAttribute('href')); }
        });
    });

    /* map the current/entered URL path to a section, on load and on back/forward */
    const pathMap = {};
    document.querySelectorAll('a[data-target]').forEach(a => {
        const p = a.getAttribute('href').replace(/\/+$/, '') || '/';
        pathMap[p] = a.dataset.target;
    });
    function goToPath(behavior) {
        let p = window.location.pathname.replace(/\/+$/, '') || '/';
        const id = pathMap[p];
        const target = id ? document.getElementById(id) : null;
        if (target) scrollToSection(target);
    }
    goToPath('auto');
    window.addEventListener('popstate', () => goToPath('smooth'));

    /* hero banner rotation */
    const banners = document.querySelectorAll('.banner'), dotsBox = document.getElementById('dots');
    banners.forEach((b, i) => { const d = document.createElement('button'); if (i === 0) d.classList.add('active'); d.addEventListener('click', () => show(i)); dotsBox.appendChild(d); });
    let bi = 0;
    function show(i) {
        banners.forEach(b => b.classList.remove('active')); dotsBox.querySelectorAll('button').forEach(d => d.classList.remove('active'));
        banners[i].classList.add('active'); dotsBox.children[i].classList.add('active'); bi = i;
    }
    setInterval(() => show((bi + 1) % banners.length), 4200);

    /* profiles slide-in */
    const scrim = document.getElementById('scrim');
    const slidein = document.getElementById('slidein');
    const slideBody = document.getElementById('slideBody');
    
    // Profile Cards click event
    document.querySelectorAll('.p-card').forEach(card => {
        card.addEventListener('click', () => {
            const templateId = `bio-${card.dataset.bioId}`;
            const template = document.getElementById(templateId);
            
            if (template) {
                slideBody.innerHTML = '';
                slideBody.appendChild(template.content.cloneNode(true));
                slidein.classList.add('open'); 
                scrim.classList.add('show');
            }
        });
    });

    // Bank Button click event
    const bankBtn = document.getElementById('bank-btn');
    if (bankBtn) {
        bankBtn.addEventListener('click', () => {
            const template = document.getElementById('bank-details');
            
            if (template) {
                slideBody.innerHTML = '';
                slideBody.appendChild(template.content.cloneNode(true));
                slidein.classList.add('open'); 
                scrim.classList.add('show');
            }
        });
    }
    function closeSlide() { slidein.classList.remove('open'); scrim.classList.remove('show'); }
    document.getElementById('slideClose').addEventListener('click', closeSlide);
    scrim.addEventListener('click', () => { closeSlide(); closeLB(); });

    /* grids (plain HTML and CSS Grid with inline icons — no external library required) */
    const galGrid=document.getElementById('galGrid'), actGrid=document.getElementById('actGrid');
    function wireFilters(box, grid){
      box.querySelectorAll('button').forEach(btn=>btn.addEventListener('click',()=>{
        box.querySelectorAll('button').forEach(b=>b.classList.remove('active')); btn.classList.add('active');
        const f=btn.dataset.f;
        grid.querySelectorAll('.iso-item').forEach(item=>{
          const match = f==='*' || ('.'+item.dataset.cat)===f;
          item.classList.toggle('hide', !match);
        });
      }));
    }
    wireFilters(document.getElementById('galFilters'), galGrid);
    wireFilters(document.getElementById('actFilters'), actGrid);

    /* lightbox slider — click any gallery/activity card to flip through its photo collection.
   Cards keep their icon as the default grid render; this just supplies the popped-out
   collection. Real photos live in the markup itself: each .iso-item can hold a hidden
   .iso-images block of <img> tags — see "Morning Assembly" and "Classroom Block" above
   for a worked example. Cards with no .iso-images (or none supplied) fall back to
   generated placeholder frames carrying the card's icon, so nothing breaks while photos
   are still being gathered. */
    const lb=document.getElementById('lightbox'), lbSlidesEl=document.getElementById('lbSlides'),
          lbDotsEl=document.getElementById('lbDots'), lbCounterEl=document.getElementById('lbCounter'),
          lbPrevBtn=document.getElementById('lbPrev'), lbNextBtn=document.getElementById('lbNext');
    const SLIDE_COUNT=4;
    let lbState={slides:[],index:0,label:'',icon:''};
    
    function hashStr(s){let h=0; for(let i=0;i<s.length;i++){h=(h*31+s.charCodeAt(i))|0;} return Math.abs(h);}
    
    function buildSlides(item, label){
      const imgs=item.querySelectorAll('.iso-images img');
      if(imgs.length) return Array.from(imgs).map(img=>({img:{src:img.getAttribute('src'), alt:img.getAttribute('alt')||label}}));
      const grads=['b1','b2','b3','b4','b5'];
      const offset=hashStr(label)%grads.length;
      return Array.from({length:SLIDE_COUNT},(_,i)=>({grad:grads[(offset+i)%grads.length]}));
    }
    
    function renderSlider(){
      lbSlidesEl.innerHTML=lbState.slides.map((s,i)=>{
        const active=i===lbState.index?' active':'';
        if(s.img) return `<div class="lb-slide${active}"><img src="${s.img.src}" alt="${s.img.alt||lbState.label}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;"></div>`;
        return `<div class="lb-slide ${s.grad}${active}">${lbState.icon?`<svg class="lb-icon" viewBox="0 0 24 24">${lbState.icon}</svg>`:''}<span class="lb-cap">${lbState.label}</span></div>`;
      }).join('');
      lbDotsEl.innerHTML=lbState.slides.map((_,i)=>`<button data-i="${i}" class="${i===lbState.index?'active':''}" aria-label="Go to image ${i+1}"></button>`).join('');
      lbCounterEl.textContent=`${lbState.index+1} / ${lbState.slides.length}`;
    }
    
    function openSlider(box){
      const item=box.closest('.iso-item');
      const label=box.dataset.label||'';
      const iconEl=box.querySelector('.iso-icon');
      lbState={slides:buildSlides(item, label), index:0, label, icon:iconEl?iconEl.innerHTML:''};
      renderSlider();
      lb.classList.add('open');
    }
    function closeLB(){ lb.classList.remove('open'); }
    function stepSlide(dir){
      if(!lbState.slides.length) return;
      lbState.index=(lbState.index+dir+lbState.slides.length)%lbState.slides.length;
      renderSlider();
    }
    
    document.addEventListener('click',(e)=>{
      const box=e.target.closest('.iso-item .box');
      if(box){ openSlider(box); }
    });
    document.getElementById('lbClose').addEventListener('click',closeLB);
    lb.addEventListener('click',(e)=>{ if(e.target===lb) closeLB(); });
    lbPrevBtn.addEventListener('click',()=>stepSlide(-1));
    lbNextBtn.addEventListener('click',()=>stepSlide(1));
    lbDotsEl.addEventListener('click',(e)=>{
      const b=e.target.closest('button[data-i]');
      if(b){ lbState.index=+b.dataset.i; renderSlider(); }
    }); 
    document.addEventListener('keydown',(e)=>{
      if(!lb.classList.contains('open')) return;
      if(e.key==='ArrowRight') stepSlide(1);
      else if(e.key==='ArrowLeft') stepSlide(-1);
      else if(e.key==='Escape') closeLB();
    });

    /* back to top */
    const backTop = document.getElementById('backTop');
    window.addEventListener('scroll', () => {
        if (window.scrollY > window.innerHeight * 0.6) backTop.classList.add('show'); else backTop.classList.remove('show');
    }, { passive: true });
    backTop.addEventListener('click', () => scrollToSection(document.getElementById('hero')));

    /* cookies banner */
    try {
        const cc = document.getElementById('cookies-directive');
        if (!localStorage.getItem('bjs_cookies_ack')) { setTimeout(() => cc.classList.add('show'), 900); }
        document.getElementById('ccClose').addEventListener('click', () => { cc.classList.remove('show'); localStorage.setItem('bjs_cookies_ack', '1'); });
    } catch (e) { }

    /* contact form — vanilla JS equivalent of processEnqForm */
    (function () {
      const form = document.getElementById('contact-form');
      if (!form) return;
    
      const resultBox   = document.getElementById('contact-result');
      const captchaMsgEl = document.getElementById('captcha-invalid'); // optional, if you add one
    
      // Maps a server PropertyName (e.g. "contact.fullname", "Contact.Email", "message")
      // to the input id actually used in the markup, since the ids here (nm/em/ms)
      // don't match the model property names 1:1 like the old Keydutor form did.
      const FIELD_MAP = {
        fullname: 'nm',
        email: 'em',
        message: 'ms'
      };
    
      function resolveFieldId(propertyName) {
        const key = propertyName.split('.').pop().toLowerCase();
        return FIELD_MAP[key] || null;
      }
    
      function clearFieldErrors() {
        form.querySelectorAll('.input-validation-error')
          .forEach(el => el.classList.remove('input-validation-error'));
      }
    
      function showOverlay() {
        const overlay = document.createElement('div');
        overlay.className = 'form-overlay';
        form.appendChild(overlay);
        return overlay;
      }
    
      function scrollToResult() {
        const anchor = (window.innerWidth < 480 && document.getElementById('bio-contact-panel')) || form.closest('section') || form;
        anchor.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    
      function showResult(message, variant) {
        if (!resultBox) return;
        resultBox.innerHTML = message;
        resultBox.classList.remove('hidden', 'alert-success', 'alert-danger');
        resultBox.classList.add('alert', variant === 'success' ? 'alert-success' : 'alert-danger');
        resultBox.style.display = '';
      }
    
      function processResult(result) {
        clearFieldErrors();
    
        if (result.successful) {
          scrollToResult();
          form.style.display = 'none';
          showResult(result.message, 'success');
          return;
        }
    
        (result.Errors || []).forEach(err => {
          const fieldId = resolveFieldId(err.PropertyName || '');
          const field = fieldId ? document.getElementById(fieldId) : null;
          if (field) field.classList.add('input-validation-error');
        });
    
        showResult(result.message, 'danger');
    
        if (result.captchaInvalid && captchaMsgEl) {
          captchaMsgEl.classList.add('field-validation-error');
          captchaMsgEl.textContent = result.captchaInvalidMessage || '';
          captchaMsgEl.style.display = '';
        }
      }
    
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        e.stopPropagation();
    
        // Native browser validation stands in for jQuery Validate here.
        // It reads the same `required`/`type=email` constraints already on
        // the inputs; it won't read ASP.NET's data-val-* attributes the way
        // jquery.validate.unobtrusive did, so add matching `pattern`/`minlength`
        // etc. directly on the inputs if you need stricter client-side rules.
        if (!form.checkValidity()) {
          form.reportValidity();
          return;
        }
    
        const overlay = showOverlay();
    
        try {
          const response = await fetch(form.getAttribute('action') || window.location.href, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
          });
          const result = await response.json();
          processResult(result);
        } catch (err) {
          showResult('Something went wrong sending your message. Please try again.', 'danger');
        } finally {
          overlay.remove();
        }
      });
    })();
} catch (err) { console.error(err); }