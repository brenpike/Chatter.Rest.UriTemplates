(function () {
  "use strict";
  var root = document.documentElement;
  var still = window.matchMedia("(prefers-reduced-motion: reduce)");
  var stage = document.getElementById("stage");
  var wrap = stage.parentElement;
  var statusEl = document.getElementById("status");
  var bar = document.getElementById("progress");
  var beats = Array.prototype.slice.call(document.querySelectorAll(".beat"));
  var scenes = Array.prototype.slice.call(stage.querySelectorAll(".scene"));
  var chars = Array.prototype.slice.call(stage.querySelectorAll(".machine .c"));
  var ledger = Array.prototype.slice.call(document.querySelectorAll("#ledger .lg"));
  var opCards = Array.prototype.slice.call(document.querySelectorAll("#ops .opc"));
  var opDetail = document.getElementById("opDetail");
  var nodes = Array.prototype.slice.call(document.querySelectorAll("#pipe .node"));
  var pipe = document.getElementById("pipe");
  var pipeCap = document.getElementById("pipeCap");
  var phone = window.matchMedia("(max-width: 860px)");

  function el(tag, cls, text) { var e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; }
  function hues() {
    var cs = getComputedStyle(root);
    return ["--l1", "--l2", "--l3", "--l4"].map(function (t) { return cs.getPropertyValue(t).trim(); });
  }

  /* ---------- drifting glyph field ---------- */
  (function glyphField() {
    if (still.matches) return;
    var cv = document.getElementById("glyphs"), ctx = cv.getContext("2d"), dpr = Math.min(2, window.devicePixelRatio || 1), W, H, items = [];
    function size() { W = cv.width = innerWidth * dpr; H = cv.height = innerHeight * dpr; }
    size(); window.addEventListener("resize", size);
    var cols = hues(), glyphs = "{}+#./;?&:*";
    var count = innerWidth < 700 ? 16 : 30;
    for (var i = 0; i < count; i++) items.push({ x: Math.random() * W, y: Math.random() * H, v: (.15 + Math.random() * .35) * dpr, s: (12 + Math.random() * 22) * dpr, g: glyphs[i % glyphs.length], c: i % 4, r: Math.random() * 6, w: (Math.random() - .5) * .004 });
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", function () { cols = hues(); });
    new MutationObserver(function () { cols = hues(); }).observe(root, { attributes: true, attributeFilter: ["data-theme"] });
    function tick() {
      if (!document.hidden) {
        ctx.clearRect(0, 0, W, H);
        ctx.globalAlpha = .16;
        items.forEach(function (it) {
          it.y -= it.v; it.r += it.w;
          if (it.y < -40 * dpr) { it.y = H + 40 * dpr; it.x = Math.random() * W; }
          ctx.save(); ctx.translate(it.x, it.y); ctx.rotate(it.r);
          ctx.fillStyle = cols[it.c]; ctx.font = "600 " + it.s + "px JetBrains Mono, monospace";
          ctx.fillText(it.g, 0, 0); ctx.restore();
        });
      }
      requestAnimationFrame(tick);
    }
    requestAnimationFrame(tick);
  })();

  /* ---------- particle bursts ---------- */
  var fx = document.createElement("canvas");
  fx.setAttribute("aria-hidden", "true");
  fx.style.cssText = "position:fixed;inset:0;width:100%;height:100%;pointer-events:none;z-index:40";
  document.body.appendChild(fx);
  var ctx = fx.getContext("2d"), parts = [], running = false, dpr = 1;
  var GLYPHS = "{}/?&=;.#*%+";
  function sizeFx() {
    dpr = Math.min(2, window.devicePixelRatio || 1);
    fx.width = innerWidth * dpr; fx.height = innerHeight * dpr;
  }
  sizeFx();
  function burst(els, n, speed, color) {
    if (still.matches) return;
    var pal = hues();
    els.forEach(function (node) {
      var r = node.getBoundingClientRect();
      if (r.bottom < 0 || r.top > innerHeight) return;
      for (var k = 0; k < n; k++) {
        var a = Math.random() * Math.PI * 2, s = speed * (0.35 + Math.random());
        parts.push({
          x: r.left + Math.random() * r.width, y: r.top + Math.random() * r.height,
          vx: Math.cos(a) * s, vy: Math.sin(a) * s - speed * 0.3,
          life: 1, decay: 0.012 + Math.random() * 0.02,
          size: 10 + Math.random() * 10, rot: Math.random() * 6, vr: (Math.random() - 0.5) * 0.3,
          ch: Math.random() < 0.6 ? GLYPHS[Math.floor(Math.random() * GLYPHS.length)] : null,
          c: color || pal[Math.floor(Math.random() * pal.length)]
        });
      }
    });
    if (!running) { running = true; requestAnimationFrame(tick); }
  }
  function tick() {
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, innerWidth, innerHeight);
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    parts = parts.filter(function (p) { return p.life > 0; });
    parts.forEach(function (p) {
      p.vy += 0.12; p.vx *= 0.985; p.x += p.vx; p.y += p.vy; p.rot += p.vr; p.life -= p.decay;
      ctx.globalAlpha = Math.max(0, p.life);
      ctx.fillStyle = p.c;
      if (p.ch) {
        ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot);
        ctx.font = "600 " + p.size + "px 'JetBrains Mono', ui-monospace, monospace";
        ctx.fillText(p.ch, 0, 0); ctx.restore();
      } else {
        ctx.beginPath(); ctx.arc(p.x, p.y, p.size / 6, 0, Math.PI * 2); ctx.fill();
      }
    });
    ctx.globalAlpha = 1;
    if (parts.length) requestAnimationFrame(tick); else running = false;
  }
  var brs = Array.prototype.slice.call(stage.querySelectorAll(".machine .br"));
  var vals = Array.prototype.slice.call(stage.querySelectorAll(".machine .o.v"));
  var machine = document.getElementById("machine");

  /* ---------- scroll-driven story ---------- */
  var PHASES = {
    tpl: [], naive: ["is-naive"], fail: ["is-fail"], tokens: ["is-tokens"],
    open: ["is-open"], fill: ["is-open", "is-filled"], encode: ["is-open", "is-filled"],
    snap: ["is-filled", "is-snapped"], done: ["is-filled", "is-snapped", "is-done"]
  };
  var ALL = ["is-naive", "is-fail", "is-tokens", "is-open", "is-filled", "is-snapped", "is-done", "is-marks"];
  var active = -1, encoded = -1, opIdx = -1, ticking = false;

  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }

  function setEncoded(n) {
    if (n === encoded) return;
    chars.forEach(function (c, i) {
      var on = i < n;
      if (on && !c.classList.contains("on")) {
        c.classList.add("fresh");
        setTimeout(function () { c.classList.remove("fresh"); }, 700);
        if (encoded >= 0 && n - encoded <= 2) setTimeout(function () { burst([c], 8, 3, getComputedStyle(root).getPropertyValue("--good").trim()); }, 60);
      }
      c.classList.toggle("on", on);
      if (ledger[i]) ledger[i].classList.toggle("on", on);
    });
    encoded = n;
  }

  function setOp(i) {
    if (i === opIdx) return;
    opIdx = i;
    opCards.forEach(function (c, k) { c.classList.toggle("on", k === i); });
    var c = opCards[i];
    opDetail.textContent = "";
    var ex = el("code", null, c.dataset.ex);
    var ar = el("span", "arrow", "→");
    var rs = el("span", "res", c.dataset.res);
    var nt = el("span", "note", c.dataset.note);
    opDetail.append(ex, ar, rs, nt);
  }

  function setNode(n) {
    stage.dataset.node = n;
    nodes.forEach(function (node, k) { node.classList.toggle("on", k === n); node.classList.toggle("past", k < n); });
    pipe.style.setProperty("--pos", (n * 25) + "%");
    pipe.style.setProperty("--f", n / 4);
    pipeCap.innerHTML = nodes[n].dataset.cap.replace(/&lt;/g, "<").replace(/&gt;/g, ">");
  }

  function setBeat(i) {
    var b = beats[i], d = b.dataset;
    var prevSnapped = stage.classList.contains("is-snapped");
    var prevOpen = stage.classList.contains("is-open");
    var prevFilled = stage.classList.contains("is-filled");
    var ph = PHASES[d.phase] || [];
    if (active >= 0) {
      if (ph.indexOf("is-open") >= 0 && !prevOpen) burst(brs, 18, 6, getComputedStyle(root).getPropertyValue("--l4").trim());
      if (ph.indexOf("is-filled") >= 0 && !prevFilled) setTimeout(function () { burst(vals, 14, 4.5); }, 350);
      if (d.phase === "snap" && !prevSnapped) burst([machine], 150, 9);
    }
    beats.forEach(function (x, k) { x.classList.toggle("is-active", k === i); });
    ALL.forEach(function (c) { stage.classList.remove(c); });
    ph.forEach(function (c) { stage.classList.add(c); });
    if (d.marks) stage.classList.add("is-marks");
    scenes.forEach(function (s) { s.classList.toggle("on", s.dataset.scene === d.scene); });
    if (d.prog !== "level") stage.dataset.level = d.scene === "levels" ? 4 : 0;
    if (d.node !== undefined) setNode(+d.node);
    statusEl.textContent = d.status || "";
    if (d.phase === "snap" && !prevSnapped) {
      stage.classList.remove("flash"); void stage.offsetWidth; stage.classList.add("flash");
    }
    if (d.scene === "ops" && opIdx < 0) setOp(0);
    active = i;
  }

  function triggerY() {
    var vh = window.innerHeight;
    if (phone.matches) {
      var sb = wrap.getBoundingClientRect().bottom;
      return sb + (vh - sb) * 0.4;
    }
    return vh * 0.55;
  }

  function update() {
    ticking = false;
    var ty = triggerY();
    var idx = 0;
    for (var i = 0; i < beats.length; i++) {
      if (beats[i].getBoundingClientRect().top <= ty) idx = i;
    }
    if (idx !== active) setBeat(idx);
    var b = beats[idx], r = b.getBoundingClientRect();
    var p = clamp((ty - r.top) / Math.max(1, r.height * 0.8), 0, 1);
    var d = b.dataset;

    if (d.prog === "level") stage.dataset.level = 1 + Math.min(3, Math.floor(p * 4));
    if (d.prog === "op") setOp(Math.min(7, Math.floor(p * 8)));

    if (d.prog === "enc") setEncoded(Math.min(chars.length, Math.floor(p * (chars.length + 1))));
    else if (d.enc === "all" || d.phase === "snap" || d.phase === "done") setEncoded(chars.length);
    else setEncoded(0);

    if (d.prog === "enc") statusEl.textContent = "encoding " + encoded + "/" + chars.length;

    var max = Math.max(1, root.scrollHeight - window.innerHeight);
    bar.style.setProperty("--p", clamp(window.scrollY / max, 0, 1));
  }

  function request() { if (!ticking) { ticking = true; requestAnimationFrame(update); } }
  window.addEventListener("scroll", request, { passive: true });
  window.addEventListener("resize", function () { sizeFx(); request(); });
  update();

  /* ---------- playground ---------- */
  var VARS = {
    city: "Montréal", q: "café & crêpes", diet: ["vegan", "gluten-free"],
    "var": "value", hello: "Hello World!", path: "/foo/bar", x: "1024", y: "768",
    list: ["red", "green", "blue"], keys: { semi: ";", dot: ".", comma: "," },
    id: "42", fields: ["name", "email"], lang: "en"
  };
  var PRESETS = [
    "/menus{/city}/search{?q,diet*}",
    "/users{/id}{?fields*}{&lang}",
    "{+path}/here{#hello}",
    "map{?x,y}{&list}",
    "{/list*}{.var:3}",
    "/settings{;keys*}",
    "/search{?q,lang,page}",
    "{??x}",
    "/broken{var",
    "{?list:2}"
  ];
  var BAD = { "{??x}": 1, "/broken{var": 1, "{?list:2}": 1 };
  var pg = document.getElementById("pg-tpl"), slotsEl = document.getElementById("pg-slots");
  var outEl = document.getElementById("pg-out"), noteEl = document.getElementById("pg-note");
  var varsEl = document.getElementById("pg-vars"), presetsEl = document.getElementById("presets");

  function fmtVal(v) {
    if (typeof v === "string") return JSON.stringify(v);
    if (Array.isArray(v)) return "[" + v.map(function (x) { return JSON.stringify(x); }).join(", ") + "]";
    return "{ " + Object.keys(v).map(function (k) { return k + ": " + JSON.stringify(v[k]); }).join(", ") + " }";
  }
  function kindOf(v) { return Array.isArray(v) ? "list" : typeof v === "object" ? "map" : "string"; }
  var vrows = {};
  Object.keys(VARS).forEach(function (n) {
    var row = el("div", "vrow");
    row.appendChild(el("span", "nm", n));
    row.appendChild(el("span", "vl", fmtVal(VARS[n])));
    row.appendChild(el("span", "ty", kindOf(VARS[n])));
    varsEl.appendChild(row);
    vrows[n] = row;
  });

  var presetBtns = PRESETS.map(function (p) {
    var b = el("button", BAD[p] ? "err-preset" : null, p); b.type = "button";
    b.setAttribute("aria-pressed", "false");
    if (BAD[p]) b.title = "This one throws a FormatException";
    b.addEventListener("click", function () { pg.value = p; render(true); });
    presetsEl.appendChild(b);
    return b;
  });

  var POOL = "abcdefghijklmnopqrstuvwxyz0123456789%/?&=;.#+{}";
  function morph(node, from, to, dur, delay, done) {
    if (node._raf) cancelAnimationFrame(node._raf);
    if (node._to) clearTimeout(node._to);
    if (still.matches) { node.textContent = to; if (done) done(); return; }
    node._to = setTimeout(function () {
      var t0 = performance.now(), last = 0;
      function frame(now) {
        var p = Math.min(1, (now - t0) / dur), e = 1 - Math.pow(1 - p, 3);
        if (now - last > 38 || p === 1) {
          last = now;
          var len = Math.round(from.length + (to.length - from.length) * e), lock = Math.floor(to.length * Math.max(0, (p - .25) / .75)), s = "";
          for (var i = 0; i < len; i++) {
            if (i < lock) s += to[i];
            else if (Math.random() < .3 && from[i]) s += from[i];
            else s += POOL[(Math.random() * POOL.length) | 0];
          }
          node.textContent = p === 1 ? to : s;
        }
        if (p < 1) node._raf = requestAnimationFrame(frame); else { node._raf = 0; if (done) done(); }
      }
      node._raf = requestAnimationFrame(frame);
    }, delay || 0);
  }

  function opLevel(ex) { return UT.OPS[ex.op].level; }
  function hasMod(ex) { return ex.vars.some(function (v) { return v.prefix != null || v.explode; }); }

  function showError(e) {
    slotsEl.textContent = "";
    outEl.className = "pg-out err";
    outEl.textContent = "";
    outEl.appendChild(el("b", null, "FormatException · thrown by " + (e.phase === "expand" ? "Expand" : "the UriTemplate constructor")));
    outEl.appendChild(document.createTextNode(e.message));
    noteEl.textContent = e.phase === "expand" ? "The template parsed fine. This value can't be expanded with that modifier." : "Nothing is expanded. A malformed template never becomes a UriTemplate.";
    Object.keys(vrows).forEach(function (n) { vrows[n].classList.remove("used"); });
  }

  var renderTimer = 0;
  function render(animate) {
    var t = pg.value;
    presetBtns.forEach(function (b) { b.setAttribute("aria-pressed", b.textContent === t ? "true" : "false"); });
    var toks, pieces;
    try {
      toks = UT.parse(t);
      pieces = toks.map(function (tk) { return tk.expr ? UT.expandExpr(tk.expr, VARS) : tk.lit; });
    } catch (e) {
      if (!(e instanceof UT.UTError)) throw e;
      showError(e);
      return;
    }

    var used = {}, missing = [];
    toks.forEach(function (tk) {
      if (!tk.expr) return;
      tk.expr.vars.forEach(function (v) {
        if (VARS[v.name] !== undefined) used[v.name] = 1;
        else if (missing.indexOf(v.name) < 0) missing.push(v.name);
      });
    });
    Object.keys(vrows).forEach(function (n, i) {
      var was = vrows[n].classList.contains("used");
      vrows[n].classList.toggle("used", !!used[n]);
      if (used[n] && animate && !still.matches) {
        vrows[n].classList.remove("ping"); void vrows[n].offsetWidth;
        vrows[n].style.animationDelay = (i * 40) + "ms";
        vrows[n].classList.add("ping");
      } else if (!was) vrows[n].classList.remove("ping");
    });
    noteEl.textContent = "";
    if (missing.length) {
      missing.forEach(function (n, i) {
        if (i) noteEl.appendChild(document.createTextNode(", "));
        noteEl.appendChild(el("code", null, n));
      });
      noteEl.appendChild(document.createTextNode(missing.length > 1 ? " are not defined, so they drop out along with their separators." : " is not defined, so it drops out along with its separator."));
    } else {
      noteEl.textContent = toks.length + " token" + (toks.length === 1 ? "" : "s") + ": " +
        toks.filter(function (x) { return !x.expr; }).length + " literal, " +
        toks.filter(function (x) { return x.expr; }).length + " expression.";
    }

    slotsEl.textContent = "";
    outEl.className = "pg-out";
    outEl.textContent = "";
    var exprEls = [], k = 0;
    toks.forEach(function (tk, i) {
      var slot = el("div", "slot" + (tk.expr ? " expr t" + opLevel(tk.expr) : " lit"));
      var tok;
      if (tk.expr) {
        var mod = hasMod(tk.expr);
        tok = el("span", "tok t" + opLevel(tk.expr) + (mod ? " t4" : ""));
        tok.appendChild(el("small", null, UT.OPS[tk.expr.op].name + (mod ? " + modifier" : "")));
      } else {
        tok = el("span", "tok lit");
        tok.appendChild(el("small", null, "literal"));
      }
      tok.appendChild(document.createTextNode(tk.raw));
      slot.appendChild(tok);
      slot.appendChild(el("span", "down", "↓"));
      var exp = el("span", "exp" + (pieces[i] === "" ? " empty" : ""), pieces[i] === "" ? "(drops out)" : pieces[i]);
      slot.appendChild(exp);
      slotsEl.appendChild(slot);

      var seg = el("span", tk.expr ? "seg t" + opLevel(tk.expr) : "seg-lit", pieces[i]);
      outEl.appendChild(seg);

      if (animate && !still.matches) {
        var delay = 140 + k * 140; k++;
        slot.style.animationDelay = delay + "ms";
        exp.style.animationDelay = delay + "ms"; tok.style.animationDelay = delay + "ms";
        slot.classList.add("pop");
        if (tk.expr) {
          morph(exp, tk.raw, pieces[i] === "" ? "(drops out)" : pieces[i], 650, delay);
          morph(seg, tk.raw, pieces[i], 650, delay);
          exprEls.push({ node: exp, delay: delay });
        }
      }
    });
    if (animate && !still.matches) {
      exprEls.forEach(function (x) { setTimeout(function () { burst([x.node], 10, 3.5); }, x.delay + 520); });
      var end = 140 + k * 140 + 700;
      setTimeout(function () { outEl.classList.remove("flash"); void outEl.offsetWidth; outEl.classList.add("flash"); }, end);
    }
  }

  pg.addEventListener("input", function () {
    clearTimeout(renderTimer);
    renderTimer = setTimeout(function () { render(true); }, 280);
  });
  render(false);

  var played = false;
  if ("IntersectionObserver" in window) {
    new IntersectionObserver(function (entries, obs) {
      entries.forEach(function (en) {
        if (en.isIntersecting && !played) { played = true; obs.disconnect(); render(true); }
      });
    }, { threshold: 0.35 }).observe(document.querySelector(".play"));
  }

  /* ---------- copy ---------- */
  var btn = document.getElementById("copyBtn");
  var cmd = document.getElementById("installCmd");
  btn.addEventListener("click", function () {
    var text = cmd.textContent;
    function selectIt() {
      var range = document.createRange(); range.selectNodeContents(cmd);
      var sel = window.getSelection(); sel.removeAllRanges(); sel.addRange(range);
      document.getElementById("copyMsg").textContent = "Selected. Copy it with your device's copy command.";
    }
    try {
      navigator.clipboard.writeText(text).then(function () { btn.textContent = "Copied"; setTimeout(function () { btn.textContent = "Copy"; }, 1600); }, selectIt);
    } catch (e) { selectIt(); }
  });
})();
