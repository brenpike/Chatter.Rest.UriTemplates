/* RFC 6570 Level 4 expander. Associative-array keys are sorted ordinally, as the library does. */
var UT = (function () {
  "use strict";
  var OPS = {
    "":  { first: "",  sep: ",", named: false, ifemp: "",  reserved: false, name: "simple string", level: 1 },
    "+": { first: "",  sep: ",", named: false, ifemp: "",  reserved: true,  name: "reserved", level: 2 },
    "#": { first: "#", sep: ",", named: false, ifemp: "",  reserved: true,  name: "fragment", level: 2 },
    ".": { first: ".", sep: ".", named: false, ifemp: "",  reserved: false, name: "label", level: 3 },
    "/": { first: "/", sep: "/", named: false, ifemp: "",  reserved: false, name: "path segment", level: 3 },
    ";": { first: ";", sep: ";", named: true,  ifemp: "",  reserved: false, name: "path parameter", level: 3 },
    "?": { first: "?", sep: "&", named: true,  ifemp: "=", reserved: false, name: "query", level: 3 },
    "&": { first: "&", sep: "&", named: true,  ifemp: "=", reserved: false, name: "query continuation", level: 3 }
  };
  var UNRES = /[A-Za-z0-9\-._~]/;
  var RES = /[:\/?#\[\]@!$&'()*+,;=]/;
  var opts = { sortKeys: true };
  function UTError(message, phase) { this.message = message; this.phase = phase || "parse"; }
  UTError.prototype = Object.create(Error.prototype);
  UTError.prototype.name = "UTError";
  function pct(ch) {
    var b = new TextEncoder().encode(ch), s = "";
    for (var i = 0; i < b.length; i++) s += "%" + (b[i] < 16 ? "0" : "") + b[i].toString(16).toUpperCase();
    return s;
  }
  function checkScalars(s) {
    for (var i = 0; i < s.length; i++) {
      var c = s.charCodeAt(i);
      if (c >= 0xD800 && c <= 0xDBFF) { var d = s.charCodeAt(i + 1); if (d >= 0xDC00 && d <= 0xDFFF) { i++; continue; } }
      if (c >= 0xD800 && c <= 0xDFFF) throw new UTError("A value contains a broken Unicode character and cannot be encoded.", "expand");
    }
  }
  function enc(s, reserved) {
    checkScalars(s);
    var out = "", cps = Array.from(s);
    for (var i = 0; i < cps.length; i++) {
      var ch = cps[i];
      if (UNRES.test(ch)) out += ch;
      else if (reserved && RES.test(ch)) out += ch;
      else if (reserved && ch === "%" && /^[0-9A-Fa-f]{2}$/.test((cps[i + 1] || "") + (cps[i + 2] || ""))) { out += ch + cps[i + 1] + cps[i + 2]; i += 2; }
      else out += pct(ch);
    }
    return out;
  }
  var LIT_BAD = " \"<>\\^`{|}";
  function inUcs(cp) {
    return (cp >= 0xA0 && cp <= 0xD7FF) || (cp >= 0xF900 && cp <= 0xFDCF) || (cp >= 0xFDF0 && cp <= 0xFFEF) ||
      (cp >= 0x10000 && cp <= 0xEFFFD && (cp & 0xFFFF) <= 0xFFFD) || (cp >= 0xE000 && cp <= 0xF8FF) ||
      (cp >= 0xF0000 && cp <= 0x10FFFD && (cp & 0xFFFF) <= 0xFFFD);
  }
  function literal(text, at) {
    var out = "", p = at, cps = Array.from(text);
    for (var i = 0; i < cps.length; i++, p++) {
      var ch = cps[i], cp = ch.codePointAt(0);
      if (ch === "}") throw new UTError("There's a closing } at position " + (p + 1) + " with no { before it.");
      if (LIT_BAD.indexOf(ch) >= 0 || cp < 0x21 || cp === 0x7F) throw new UTError("The " + (ch === " " ? "space" : "character " + ch) + " at position " + (p + 1) + " isn't allowed in a URI. Percent-encode it or move it into a variable.");
      if (ch === "%") {
        if (!/^[0-9A-Fa-f]{2}$/.test((cps[i + 1] || "") + (cps[i + 2] || ""))) throw new UTError("The % at position " + (p + 1) + " must start a percent-encoded triplet like %20.");
        out += ch + cps[i + 1] + cps[i + 2]; i += 2; p += 2; continue;
      }
      if (cp > 0x7F) { if (!inUcs(cp)) throw new UTError("The character at position " + (p + 1) + " can't appear in a URI template."); out += pct(ch); continue; }
      out += ch;
    }
    return out;
  }
  var VARNAME = /^(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})+(?:\.(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})+)*$/;
  function parseExpr(body, at) {
    if (body === "") throw new UTError("The expression at position " + (at + 1) + " is empty. Put a variable name inside the braces.");
    var op = "";
    if ("+#./;?&".indexOf(body[0]) >= 0) { op = body[0]; body = body.slice(1); }
    else if ("=,!@|".indexOf(body[0]) >= 0) throw new UTError("The operator \"" + body[0] + "\" is reserved by RFC 6570 for future use.");
    if (body === "") throw new UTError("The expression at position " + (at + 1) + " has an operator but no variable.");
    if (op && "+#./;?&=,!@|".indexOf(body[0]) >= 0) throw new UTError("The expression at position " + (at + 1) + " has two operators. Only one is allowed, right after the {.");
    var vars = body.split(",").map(function (spec) {
      var m, name = spec, explode = false, prefix = null;
      if (spec.slice(-1) === "*") { explode = true; name = spec.slice(0, -1); }
      else if ((m = /^(.*):(.*)$/.exec(spec))) {
        name = m[1];
        if (!/^[1-9][0-9]{0,3}$/.test(m[2])) throw new UTError("The prefix \":" + m[2] + "\" must be a whole number from 1 to 9999.");
        prefix = +m[2];
      }
      if (name.indexOf(":") >= 0 || name.indexOf("*") >= 0) throw new UTError("\"" + spec + "\" uses both a prefix and explode. Pick one.");
      if (!VARNAME.test(name)) throw new UTError(name === "" ? "A variable name is missing between commas." : "\"" + name + "\" isn't a valid variable name. Use letters, digits, _ and dots.");
      return { name: name, explode: explode, prefix: prefix };
    });
    return { op: op, vars: vars };
  }
  function parse(tpl) {
    var tokens = [], i = 0;
    while (i < tpl.length) {
      var open = tpl.indexOf("{", i);
      if (open === -1) { tokens.push({ lit: literal(tpl.slice(i), i), raw: tpl.slice(i) }); break; }
      if (open > i) tokens.push({ lit: literal(tpl.slice(i, open), i), raw: tpl.slice(i, open) });
      var close = tpl.indexOf("}", open), nested = tpl.indexOf("{", open + 1);
      if (close === -1) throw new UTError("The { at position " + (open + 1) + " is never closed. Add a } to finish the expression.");
      if (nested !== -1 && nested < close) throw new UTError("Expressions can't be nested. There's a { inside the expression at position " + (open + 1) + ".");
      tokens.push({ expr: parseExpr(tpl.slice(open + 1, close), open), raw: tpl.slice(open, close + 1) });
      i = close + 1;
    }
    return tokens;
  }
  function kind(v) { return v == null ? "undef" : Array.isArray(v) ? "list" : typeof v === "object" ? "map" : "string"; }
  function pairs(v) {
    var keys = Object.keys(v);
    if (opts.sortKeys) keys.sort(function (a, b) { return a < b ? -1 : a > b ? 1 : 0; });
    return keys.map(function (k) { return [k, String(v[k])]; });
  }
  function expandExpr(ex, values) {
    var o = OPS[ex.op], parts = [];
    ex.vars.forEach(function (vs) {
      var v = values[vs.name], k = kind(v);
      if (k === "undef" || (k === "list" && v.length === 0) || (k === "map" && Object.keys(v).length === 0)) return;
      if (vs.prefix != null && k !== "string") throw new UTError("\"" + vs.name + "\" is a " + (k === "list" ? "list" : "dictionary") + ". A prefix like :" + vs.prefix + " only works on strings.", "expand");
      var e = function (s) { return enc(s, o.reserved); };
      if (k === "string") {
        var s = String(v);
        if (vs.prefix != null) s = Array.from(s).slice(0, vs.prefix).join("");
        parts.push(o.named ? (s === "" ? vs.name + o.ifemp : vs.name + "=" + e(s)) : e(s));
      } else if (!vs.explode) {
        var flat = k === "list" ? v.map(function (x) { return e(String(x)); })
          : pairs(v).reduce(function (acc, p) { acc.push(e(p[0]), e(p[1])); return acc; }, []);
        var j = flat.join(",");
        parts.push(o.named ? vs.name + (j === "" ? o.ifemp : "=" + j) : j);
      } else if (k === "list") {
        parts.push(v.map(function (x) { x = String(x); return o.named ? (x === "" ? vs.name + o.ifemp : vs.name + "=" + e(x)) : e(x); }).join(o.sep));
      } else {
        parts.push(pairs(v).map(function (p) { return o.named ? (p[1] === "" ? e(p[0]) + o.ifemp : e(p[0]) + "=" + e(p[1])) : e(p[0]) + "=" + e(p[1]); }).join(o.sep));
      }
    });
    return parts.length ? o.first + parts.join(o.sep) : "";
  }
  function expand(tpl, values) {
    return parse(tpl).map(function (t) { return t.expr ? expandExpr(t.expr, values) : t.lit; }).join("");
  }
  return { OPS: OPS, parse: parse, expand: expand, expandExpr: expandExpr, UTError: UTError, opts: opts };
})();
if (typeof module !== "undefined") module.exports = UT;
