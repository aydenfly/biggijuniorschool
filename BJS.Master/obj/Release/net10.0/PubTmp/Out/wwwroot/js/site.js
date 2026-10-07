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
    const bios = [
        {
            n: 'Hellen Nyilak', r: 'Founder & Director', t: ["Hellen Nyilak founded Biggi Junior School with a simple conviction: every child in Pakwach deserves a strong start in life, regardless of their family's means. Under her leadership, the school has grown into a nurturing home for learners from baby class through primary seven.",
                "Hellen continues to guide the school's vision, working closely with teachers, parents, and partners to ensure every child is fed, taught, and cared for. She is especially passionate about fundraising for tuition, scholastic materials, and bursaries, and warmly welcomes donors to join this journey."]
        },
        {
            n: 'Wanadi Melki', r: 'Head Teacher', t: ["As Head Teacher, Wanadi Melki oversees daily life and learning at Biggi Junior School, ensuring every classroom from baby class to primary seven runs smoothly and that pupils and staff are supported to do their best work.",
                "Wanadi works closely with teachers to maintain high standards of teaching and welfare, and is a strong advocate for the school's bursary and fundraising programs, knowing how access to basic materials can keep a child in school."]
        },
        {
            n: 'Ocakwun Fred', r: 'Dean of Studies', t: ["Ocakwun Fred serves as Dean of Studies, responsible for shaping and monitoring the academic program across all classes, working closely with teachers to plan curriculum delivery and track pupil performance.",
                "Fred is committed to raising academic standards while ensuring no child falls behind for lack of resources, and believes strongly in community partnerships and donor support to keep every learner progressing."]
        },
        {
            n: 'Pikisa Kenneth', r: 'Head Prefect', t: ["Pikisa Kenneth serves as Head Prefect, representing fellow pupils and helping keep order, discipline, and school spirit alive both in and out of the classroom, setting a strong example in punctuality and respect.",
                "Kenneth helps bridge the gap between pupils and staff, ensuring student concerns are heard — a leadership style that reflects the values Biggi hopes to instill in every child."]
        },
        {
            n: 'Yikparwoth Fortunate', r: 'Head Girl', t: ["Yikparwoth Fortunate proudly serves as Head Girl, guiding fellow girl pupils with confidence and warmth, often the first to encourage younger girls to stay focused on their studies and believe in what they can achieve.",
                "Fortunate's leadership is a reminder of why continued support for tuition and scholastic materials matters — every girl she mentors deserves the same chance to learn, lead, and dream big."]
        },
        {
            n: 'Mungudit Elvis', r: 'Head Boy', t: ["Mungudit Elvis holds the position of Head Boy, looked up to by fellow pupils for his discipline, leadership, and dedication to his studies, playing an active role in maintaining order around the school.",
                "Elvis represents the promise of every child at Biggi Junior School — a promise that depends on continued donor support to keep classrooms stocked, tuition covered, and doors open for pupils like him."]
        }
    ];
    const scrim = document.getElementById('scrim'), slidein = document.getElementById('slidein'), slideBody = document.getElementById('slideBody');
    document.querySelectorAll('.p-card').forEach(c => c.addEventListener('click', () => {
        const p = bios[+c.dataset.p];
        slideBody.innerHTML = `<h3>${p.n}</h3><span class="role">${p.r}</span>` + p.t.map(x => `<p>${x}</p>`).join('');
        slidein.classList.add('open'); scrim.classList.add('show');
    }));

    const bankBtn = document.getElementById('bank-btn');
    if (bankBtn) {
        bankBtn.addEventListener('click', () => {
            slideBody.innerHTML = `<h3>Cheque or Bank Payments</h3>
      <p>Your contribution towards supporting bursaries is always welcome. If you wish to contribute via Cheque or Bank payments, please pay into the following account.</p>
      <div class="bank-details">
        <div><span>Bank:</span> <b>Absa Uganda, Luwum Street Branch</b></div>
        <div><span>Account Name:</span> <b>Biggi Family Project</b></div>
        <div><span>Account Number (UGX):</span> <b>6005548274</b></div>
        <div><span>Account Number (USD):</span> <b>6005548266</b></div>
        <div><span>SWIFT Code:</span> <b>BARCUGKX</b></div>
      </div>`;
            slidein.classList.add('open'); scrim.classList.add('show');
        });
    }
    function closeSlide() { slidein.classList.remove('open'); scrim.classList.remove('show'); }
    document.getElementById('slideClose').addEventListener('click', closeSlide);
    scrim.addEventListener('click', () => { closeSlide(); closeLB(); });

    /* grids (plain CSS Grid — no external library required) */
    function fills(n) { const g = ['b1', 'b2', 'b3', 'b4', 'b5']; return g[n % g.length]; }
    function buildGrid(el, items) {
        el.innerHTML = '';
        items.forEach((it, i) => {
            const d = document.createElement('div'); d.className = 'iso-item'; d.dataset.cat = it.c;
            d.innerHTML = `<div class="box ${fills(i)}" data-label="${it.l}"><span>${it.l}</span></div>`;
            el.appendChild(d);
        });
    }
    const galItems = [
        { l: 'Morning Assembly', c: 'school' }, { l: 'Baby Class', c: 'students' }, { l: 'Prize Giving Day', c: 'events' }, { l: 'Classroom Block', c: 'school' },
        { l: 'Reading Time', c: 'students' }, { l: 'Sports Day', c: 'events' }, { l: 'Primary Seven', c: 'students' }, { l: 'School Compound', c: 'school' }
    ];
    const actItems = [
        { l: 'Football Pitch', c: 'sports' }, { l: 'Library Corner', c: 'facility' }, { l: 'Debate Club', c: 'clubs' }, { l: 'Netball Court', c: 'sports' },
        { l: 'Dining Hall', c: 'facility' }, { l: 'Music Club', c: 'clubs' }, { l: 'Athletics', c: 'sports' }, { l: 'Science Corner', c: 'facility' }
    ];
    const galGrid = document.getElementById('galGrid'), actGrid = document.getElementById('actGrid');
    buildGrid(galGrid, galItems); buildGrid(actGrid, actItems);

    function wireFilters(box, grid) {
        box.querySelectorAll('button').forEach(btn => btn.addEventListener('click', () => {
            box.querySelectorAll('button').forEach(b => b.classList.remove('active')); btn.classList.add('active');
            const f = btn.dataset.f;
            grid.querySelectorAll('.iso-item').forEach(item => {
                const match = f === '*' || ('.' + item.dataset.cat) === f;
                item.classList.toggle('hide', !match);
            });
        }));
    }
    wireFilters(document.getElementById('galFilters'), galGrid);
    wireFilters(document.getElementById('actFilters'), actGrid);

    /* lightbox (fancybox-style) */
    const lb = document.getElementById('lightbox'), lbStage = document.getElementById('lbStage');
    document.addEventListener('click', (e) => {
        const box = e.target.closest('.iso-item .box');
        if (box) {
            lbStage.className = 'stage ' + [...box.classList].find(c => c.startsWith('b') && c.length === 2);
            lbStage.textContent = box.dataset.label; lb.classList.add('open');
        }
    });
    function closeLB() { lb.classList.remove('open'); }
    document.getElementById('lbClose').addEventListener('click', closeLB);
    lb.addEventListener('click', (e) => { if (e.target === lb) closeLB(); });

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