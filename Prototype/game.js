(() => {
  'use strict';

  const canvas = document.getElementById('moon');
  const ctx = canvas.getContext('2d');
  const $ = (id) => document.getElementById(id);
  const ui = {
    overlay: $('overlay'), intro: $('introModal'), briefing: $('briefingModal'), tutorial: $('tutorialModal'), pause: $('pauseModal'), report: $('reportModal'),
    facility: $('facilityPanel'), facilityTitle: $('facilityTitle'), facilityDescription: $('facilityDescription'),
    event: $('eventPanel'), toast: $('toast'), interactHint: $('interactHint'), interactName: $('interactName'), interactRange: $('interactRange')
  };

  const objectives = [
    'Activate the communications station',
    'Bring the solar array online',
    'Inspect life-support systems',
    'Resolve the power shortage',
    'Collect a virtual sample with the rover'
  ];
  const facilities = [
    { id: 'habitat', type: 'habitat', title: 'Crew habitat', x: 0, z: 10, range: 3.7, description: 'Pressurized shelter for the crew. A seal inspection can improve habitat condition.' },
    { id: 'comms', type: 'comms', title: 'Communications station', x: -9, z: 4, range: 3.4, description: 'Aim the relay and establish a signal with Mission Control.' },
    { id: 'solar', type: 'solar', title: 'Solar power station', x: 9, z: 4, range: 3.5, description: 'Deploy and align the array to supply the outpost. Output changes in this simplified simulation.' },
    { id: 'life', type: 'life', title: 'Life-support systems', x: -9, z: -7, range: 3.4, description: 'Inspect the oxygen and water loop before surface operations.' },
    { id: 'greenhouse', type: 'greenhouse', title: 'Food production greenhouse', x: 0, z: -11, range: 3.7, description: 'Harvest food when power is available. The grow lights use outpost energy.' },
    { id: 'rover', type: 'rover', title: 'Lunar exploration rover', x: 9, z: -8, range: 3.7, description: 'Collect a virtual regolith sample once the outpost is stabilized.' }
  ];
  const rockSeed = 812;
  let seed = rockSeed;
  const random = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };
  const rocks = Array.from({ length: 76 }, () => {
    let x = random() * 72 - 36, z = random() * 72 - 36;
    if (Math.abs(x) < 14 && Math.abs(z) < 17) { x += x < 0 ? -9 : 9; z += z < 0 ? -7 : 8; }
    return { x, z, size: 0.22 + random() * 0.9, shade: 43 + Math.floor(random() * 17), angle: random() * 1.2 - 0.6 };
  });
  rocks.sort((a,b)=>(a.x+a.z)-(b.x+b.z));
  const stars = Array.from({ length: 110 }, () => ({ x: random(), y: random() * 0.44, r: 0.4 + random() * 1.25, a: 0.18 + random() * 0.5 }));
  const state = {
    player: { x: 0, z: -1.5, facing: 0, jump: 0, jumpV: 0 }, camera: { angle: 0 },
    resources: { oxygen: 100, water: 100, food: 100, power: 82, habitat: 100 },
    done: [false, false, false, false, false],
    online: { comms: false, solar: false, life: false },
    running: false, paused: false, eventOpen: false, eventDone: false,
    safeChoice: false, sample: false, elapsed: 0, tick: 0, uiTick: 0, lastFrame: 0,
    held: new Set(), pointer: null, currentFacility: null, facilityDismissed: null, toastTimer: 0, ended: false
  };

  function resize() {
    const rect = canvas.getBoundingClientRect();
    const dpr = Math.min(window.devicePixelRatio || 1, 1.7);
    canvas.width = Math.max(1, Math.floor(rect.width * dpr));
    canvas.height = Math.max(1, Math.floor(rect.height * dpr));
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }
  window.addEventListener('resize', resize);
  resize();

  function projection(x, z, y = 0) {
    const dx = x - state.player.x, dz = z - state.player.z, a = state.camera.angle;
    const rx = dx * Math.cos(a) - dz * Math.sin(a);
    const rz = dx * Math.sin(a) + dz * Math.cos(a);
    const w = canvas.clientWidth, h = canvas.clientHeight;
    return { x: w * 0.5 + (rx - rz) * 24, y: h * 0.585 + (rx + rz) * 12 - y * 24 };
  }
  function polygon(points, fill, stroke = null, lineWidth = 1) {
    ctx.beginPath(); ctx.moveTo(points[0].x, points[0].y);
    for (let i = 1; i < points.length; i++) ctx.lineTo(points[i].x, points[i].y);
    ctx.closePath(); ctx.fillStyle = fill; ctx.fill();
    if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = lineWidth; ctx.stroke(); }
  }
  function box(x, z, width, depth, height, color, y = 0, detail = null) {
    const p = (dx, dz, yy) => projection(x + dx, z + dz, y + yy);
    const a = p(-width / 2, -depth / 2, 0), b = p(width / 2, -depth / 2, 0);
    const c = p(width / 2, depth / 2, 0), d = p(-width / 2, depth / 2, 0);
    const A = p(-width / 2, -depth / 2, height), B = p(width / 2, -depth / 2, height);
    const C = p(width / 2, depth / 2, height), D = p(-width / 2, depth / 2, height);
    const base = color;
    polygon([d, c, C, D], shade(base, -18));
    polygon([b, c, C, B], shade(base, -30));
    polygon([a, b, B, A], shade(base, -9));
    polygon([a, b, c, d], shade(base, -34));
    polygon([A, B, C, D], base, detail ? detail.stroke : null, 1);
    if (detail?.lines) {
      for (let i = 1; i < detail.lines; i++) {
        const t = i / detail.lines;
        const l1 = { x: A.x + (D.x - A.x) * t, y: A.y + (D.y - A.y) * t };
        const l2 = { x: B.x + (C.x - B.x) * t, y: B.y + (C.y - B.y) * t };
        ctx.beginPath(); ctx.moveTo(l1.x, l1.y); ctx.lineTo(l2.x, l2.y); ctx.strokeStyle = detail.lineColor || 'rgba(120,207,231,.28)'; ctx.lineWidth = 1; ctx.stroke();
      }
    }
    return { top: [A, B, C, D], center: projection(x, z, height) };
  }
  function shade(hex, delta) {
    const n = parseInt(hex.slice(1), 16);
    const r = Math.max(0, Math.min(255, (n >> 16) + delta));
    const g = Math.max(0, Math.min(255, ((n >> 8) & 255) + delta));
    const b = Math.max(0, Math.min(255, (n & 255) + delta));
    return `rgb(${r},${g},${b})`;
  }
  function ellipse(x, z, rx, rz, y, color, alpha = 1) {
    const c = projection(x, z, y), scale = 24;
    ctx.save(); ctx.globalAlpha = alpha; ctx.beginPath();
    ctx.ellipse(c.x, c.y, Math.max(1, rx * scale), Math.max(1, rz * scale * 0.5), -Math.PI / 4, 0, Math.PI * 2);
    ctx.fillStyle = color; ctx.fill(); ctx.restore();
  }
  function drawBackground(w, h) {
    const sky = ctx.createLinearGradient(0, 0, 0, h * 0.7);
    sky.addColorStop(0, '#090f1a'); sky.addColorStop(.48, '#172235'); sky.addColorStop(1, '#72747a');
    ctx.fillStyle = sky; ctx.fillRect(0, 0, w, h);
    for (const star of stars) {
      ctx.globalAlpha = star.a * (.72 + Math.sin(state.elapsed * .7 + star.x * 35) * .18);
      ctx.fillStyle = '#dbe9fa'; ctx.beginPath(); ctx.arc(star.x * w, star.y * h, star.r, 0, Math.PI * 2); ctx.fill();
    }
    ctx.globalAlpha = 1;
    const earth = { x: w * .77, y: h * .22 };
    const glow = ctx.createRadialGradient(earth.x, earth.y, 12, earth.x, earth.y, 60);
    glow.addColorStop(0, 'rgba(87,167,239,.28)'); glow.addColorStop(1, 'rgba(87,167,239,0)');
    ctx.fillStyle = glow; ctx.beginPath(); ctx.arc(earth.x, earth.y, 60, 0, 7); ctx.fill();
    const globe = ctx.createRadialGradient(earth.x - 8, earth.y - 10, 2, earth.x, earth.y, 25);
    globe.addColorStop(0, '#acd8ed'); globe.addColorStop(.55, '#3973a7'); globe.addColorStop(1, '#142d4d');
    ctx.fillStyle = globe; ctx.beginPath(); ctx.arc(earth.x, earth.y, 22, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.ellipse(earth.x - 4, earth.y - 4, 8, 4, -.6, 0, Math.PI * 2); ctx.fillStyle = 'rgba(210,236,241,.45)'; ctx.fill();
    const horizon = ctx.createLinearGradient(0, h * .4, 0, h * .66);
    horizon.addColorStop(0, 'rgba(184,185,188,0)'); horizon.addColorStop(1, '#777a7d');
    ctx.fillStyle = horizon; ctx.fillRect(0, h * .4, w, h * .3);
    for (let i = 0; i < 11; i++) {
      const x = (i / 10) * w, peak = h * (.52 + Math.sin(i * 1.8) * .035);
      ctx.beginPath(); ctx.moveTo(x - 100, h * .68); ctx.lineTo(x, peak); ctx.lineTo(x + 125, h * .68); ctx.closePath();
      ctx.fillStyle = i % 2 ? '#585e65' : '#65696e'; ctx.fill();
    }
  }
  function drawGround(w, h) {
    const corners = [[-42,-42], [42,-42], [42,42], [-42,42]].map(([x,z]) => projection(x,z,-.2));
    const g = ctx.createLinearGradient(0, h * .35, w, h);
    g.addColorStop(0, '#85878a'); g.addColorStop(.55, '#a1a1a0'); g.addColorStop(1, '#777a7d');
    polygon(corners, g);
    // Dusty ridges in the middle distance.
    for (let i = 0; i < 28; i++) {
      const x = (i - 14) * 3.5, z = 24 + Math.sin(i * 1.31) * 9, r = 1.2 + ((i * 7) % 9) * .25;
      ellipse(x, z, r * 2.1, r, -.08, i % 2 ? 'rgba(194,193,187,.18)' : 'rgba(55,60,65,.16)');
    }
    // Crater bowls and raised rims.
    for (const c of [{x:-24,z:14,r:6},{x:25,z:21,r:7},{x:-24,z:-23,r:8},{x:25,z:-22,r:5}]) {
      ellipse(c.x,c.z,c.r*1.25,c.r,-.04,'rgba(207,205,198,.38)');
      ellipse(c.x,c.z,c.r,c.r*.78,-.03,'rgba(49,53,58,.43)');
      ellipse(c.x-0.5,c.z-0.7,c.r*.67,c.r*.49,-.02,'rgba(35,39,44,.32)');
      ellipse(c.x-c.r*.14,c.z-c.r*.1,c.r*.9,c.r*.7,0,'rgba(232,230,220,.09)');
    }
    // Regolith grid is deliberately subtle, to help the player read scale and direction.
    for (let x = -40; x <= 40; x += 4) {
      const a = projection(x,-40,-.19), b = projection(x,40,-.19);
      ctx.beginPath(); ctx.moveTo(a.x,a.y); ctx.lineTo(b.x,b.y); ctx.strokeStyle='rgba(46,52,58,.065)'; ctx.lineWidth=1; ctx.stroke();
    }
    for (let z = -40; z <= 40; z += 4) {
      const a = projection(-40,z,-.19), b = projection(40,z,-.19);
      ctx.beginPath(); ctx.moveTo(a.x,a.y); ctx.lineTo(b.x,b.y); ctx.strokeStyle='rgba(46,52,58,.065)'; ctx.lineWidth=1; ctx.stroke();
    }
  }
  function drawRock(rock) {
    const p = projection(rock.x, rock.z, rock.size * .22), r = rock.size * 17;
    ellipse(rock.x, rock.z, rock.size*.9, rock.size*.75, -.01, 'rgba(25,29,33,.25)');
    ctx.save(); ctx.translate(p.x,p.y); ctx.rotate(rock.angle);
    ctx.beginPath(); ctx.moveTo(-r,-r*.1); ctx.lineTo(-r*.55,-r*.72); ctx.lineTo(r*.23,-r); ctx.lineTo(r*.82,-r*.45); ctx.lineTo(r,.15*r); ctx.lineTo(r*.5,r*.46); ctx.lineTo(-r*.54,r*.5); ctx.closePath();
    ctx.fillStyle=`rgb(${rock.shade},${rock.shade+1},${rock.shade+3})`;ctx.fill();ctx.strokeStyle='rgba(223,222,215,.15)';ctx.lineWidth=1;ctx.stroke();
    ctx.beginPath();ctx.moveTo(-r*.48,-r*.55);ctx.lineTo(r*.18,-r*.86);ctx.lineTo(r*.52,-r*.41);ctx.strokeStyle='rgba(237,234,222,.2)';ctx.stroke();
    ctx.restore();
  }
  function drawHabitat(f) {
    const p = f;
    ellipse(p.x,p.z,3.7,2.9,0,'rgba(28,32,35,.34)');
    box(p.x,p.z,4.5,3.1,2.1,'#969fa4');
    const dome=projection(p.x,p.z,2.15);
    ctx.beginPath();ctx.ellipse(dome.x,dome.y-10,52,28,-.16,Math.PI,Math.PI*2);ctx.fillStyle='#8999a2';ctx.fill();ctx.strokeStyle='#bdc5c5';ctx.stroke();
    box(p.x,p.z+2.15,1.7,.8,1.45,'#7e898f');
    const win=projection(p.x,p.z+2.58,1.25);ctx.fillStyle='#51d4ee';ctx.globalAlpha=.82;ctx.fillRect(win.x-15,win.y-7,30,12);ctx.globalAlpha=1;
    box(p.x-3.1,p.z,2.1,1.5,.1,'#183b5c',1.1,{lines:5,lineColor:'rgba(92,191,239,.58)'});
    box(p.x+3.1,p.z,2.1,1.5,.1,'#183b5c',1.1,{lines:5,lineColor:'rgba(92,191,239,.58)'});
    label(p,'HABITAT', '#63d9ee');
  }
  function drawComms(f) {
    ellipse(f.x,f.z,1.5,1.2,0,'rgba(24,28,32,.32)');
    box(f.x,f.z,1.6,1.25,1.4,'#879296');
    const base=projection(f.x,f.z,1.4), mast=projection(f.x,f.z,4.0);
    ctx.beginPath();ctx.moveTo(base.x,base.y);ctx.lineTo(mast.x,mast.y);ctx.strokeStyle='#c8c9bf';ctx.lineWidth=3;ctx.stroke();
    ctx.beginPath();ctx.ellipse(mast.x+7,mast.y-8,19,8,-.35,0,Math.PI*2);ctx.fillStyle='#d1d1c9';ctx.fill();ctx.strokeStyle='#87959a';ctx.stroke();
    const screen=projection(f.x,f.z+0.68,1.08);ctx.fillStyle='#30cbe5';ctx.fillRect(screen.x-12,screen.y-8,24,12);
    label(f,'COMMS RELAY','#58dcf5');
  }
  function drawSolar(f) {
    ellipse(f.x,f.z,4.4,2.9,0,'rgba(24,28,32,.27)');
    for(let i=-1;i<=1;i++){
      const x=f.x+i*2.35, p=projection(x,f.z,1.65);
      const post0=projection(x,f.z,.1), post1=projection(x,f.z,1.5);
      ctx.beginPath();ctx.moveTo(post0.x,post0.y);ctx.lineTo(post1.x,post1.y);ctx.strokeStyle='#c6c8c4';ctx.lineWidth=3;ctx.stroke();
      box(x,f.z,1.95,2.1,.12,'#173d64',1.63,{lines:4,lineColor:'rgba(115,197,237,.68)'});
      ctx.beginPath();ctx.moveTo(p.x-18,p.y-10);ctx.lineTo(p.x+18,p.y-10);ctx.strokeStyle='rgba(225,239,241,.45)';ctx.stroke();
    }
    box(f.x,f.z+2.05,1.25,.6,.75,'#8b979b');
    label(f,'SOLAR ARRAY','#79e5bd');
  }
  function drawLifeSupport(f) {
    ellipse(f.x,f.z,2.8,2.1,0,'rgba(24,28,32,.3)');
    box(f.x,f.z+1,2,1.2,1.15,'#7f8c91');
    for(const dx of [-1.25,1.25]){
      const c=projection(f.x+dx,f.z,1.3);
      ctx.fillStyle=dx<0?'#abb3b4':'#70a6b8';ctx.beginPath();ctx.ellipse(c.x,c.y,12,18,-.2,0,Math.PI*2);ctx.fill();
      ctx.strokeStyle='rgba(235,241,235,.55)';ctx.stroke();
    }
    const panel=projection(f.x,f.z+1.62,1.0);ctx.fillStyle='#3ccee0';ctx.fillRect(panel.x-15,panel.y-7,30,13);
    label(f,'LIFE SUPPORT','#79e5bd');
  }
  function drawGreenhouse(f) {
    ellipse(f.x,f.z,2.9,2.4,0,'rgba(24,28,32,.28)');
    box(f.x,f.z,4,3.25,2,'#557d82',0,{lines:5,lineColor:'rgba(108,221,226,.38)'});
    const front=projection(f.x,f.z+1.64,1.1);
    ctx.fillStyle='rgba(92,210,203,.35)';ctx.fillRect(front.x-37,front.y-25,74,40);
    for(let i=-1;i<=1;i++){
      const plant=projection(f.x+i*1.05,f.z+.65,.72);
      ctx.beginPath();ctx.ellipse(plant.x,plant.y,9,15,i*.15,0,Math.PI*2);ctx.fillStyle='#68a778';ctx.fill();
    }
    label(f,'GREENHOUSE','#f3bd67');
  }
  function drawRover(f) {
    ellipse(f.x,f.z,2.1,1.7,0,'rgba(24,28,32,.38)');
    box(f.x,f.z,2.65,1.8,.72,'#b8b5a7',.55);
    box(f.x,f.z,1.65,1.25,.42,'#888f89',1.28);
    for(const dx of [-1,1])for(const dz of [-1,1]){
      const p=projection(f.x+dx*1.28,f.z+dz*.8,.43);
      ctx.beginPath();ctx.ellipse(p.x,p.y,8,13,-.5,0,Math.PI*2);ctx.fillStyle='#20272a';ctx.fill();ctx.strokeStyle='#9eaaa7';ctx.stroke();
    }
    const mast=projection(f.x,f.z,2.7);ctx.beginPath();ctx.moveTo(projection(f.x,f.z,1.6).x,projection(f.x,f.z,1.6).y);ctx.lineTo(mast.x,mast.y);ctx.strokeStyle='#d8d3c1';ctx.lineWidth=2;ctx.stroke();
    ctx.fillStyle='#efbd5c';ctx.fillRect(mast.x-5,mast.y-4,10,8);
    label(f,'LUNAR ROVER','#f3bd67');
  }
  function label(f,text,color){
    const p=projection(f.x,f.z,5.2);const d=distance(state.player,f);
    if(d>18)return;
    ctx.save();ctx.globalAlpha=d<4.3?1:.7;ctx.font='9px monospace';ctx.textAlign='center';
    ctx.fillStyle='rgba(7,15,24,.72)';const width=ctx.measureText(text).width+16;ctx.fillRect(p.x-width/2,p.y-12,width,20);
    ctx.strokeStyle=color;ctx.globalAlpha=.55;ctx.strokeRect(p.x-width/2,p.y-12,width,20);ctx.globalAlpha=1;ctx.fillStyle=color;ctx.fillText(text,p.x,p.y+2);ctx.restore();
  }
  function drawLandingPad(){
    const f={x:0,z:-1};ellipse(f.x,f.z,5.4,4.2,-.02,'rgba(38,43,48,.54)');
    const c=projection(f.x,f.z,.02);ctx.save();ctx.translate(c.x,c.y);ctx.beginPath();ctx.ellipse(0,0,117,58,-Math.PI/4,0,Math.PI*2);ctx.fillStyle='#777d80';ctx.fill();ctx.strokeStyle='rgba(226,224,212,.5)';ctx.lineWidth=2;ctx.stroke();
    ctx.setLineDash([8,6]);ctx.beginPath();ctx.ellipse(0,0,86,42,-Math.PI/4,0,Math.PI*2);ctx.strokeStyle='rgba(225,214,183,.66)';ctx.stroke();ctx.setLineDash([]);ctx.restore();
    const L=projection(-.6,-1,.08);ctx.fillStyle='#e3dfcf';ctx.font='bold 18px monospace';ctx.fillText('A',L.x-8,L.y+7);
  }
  function drawBeacon(f, nearest){
    if(!nearest)return;
    const p=projection(f.x,f.z,5.8);
    const pulse=1+Math.sin(state.elapsed*4)*.15;
    ctx.save();ctx.globalAlpha=.6;ctx.beginPath();ctx.ellipse(p.x,p.y+9,10*pulse,4*pulse,0,0,Math.PI*2);ctx.fillStyle='#59dcf5';ctx.fill();ctx.restore();
    ctx.beginPath();ctx.moveTo(p.x,p.y+4);ctx.lineTo(p.x,p.y+13);ctx.strokeStyle='#59dcf5';ctx.lineWidth=1.5;ctx.stroke();
  }
  function drawAstronaut(){
    const p=state.player, shadow=projection(p.x,p.z,0);
    ellipse(p.x,p.z,.62,.48,-.02,'rgba(24,28,32,.44)');
    const body=projection(p.x,p.z,1.05+p.jump), head=projection(p.x,p.z,1.88+p.jump);
    ctx.save();ctx.translate(body.x,body.y);
    ctx.beginPath();ctx.ellipse(0,0,15,21,-.1,0,Math.PI*2);ctx.fillStyle='#dce1df';ctx.fill();ctx.strokeStyle='#77848a';ctx.lineWidth=1;ctx.stroke();
    ctx.fillStyle='#263441';ctx.fillRect(-16,-4,7,19);ctx.fillRect(9,-4,7,19);
    ctx.fillStyle='#d8d8ce';ctx.fillRect(-19,15,14,5);ctx.fillRect(5,15,14,5);
    ctx.fillStyle='#3b555e';ctx.fillRect(-10,-3,20,12);ctx.fillStyle='#49d7ed';ctx.fillRect(-6,0,12,5);
    ctx.restore();
    const g=ctx.createRadialGradient(head.x-4,head.y-5,2,head.x,head.y,13);g.addColorStop(0,'#c8edf1');g.addColorStop(.42,'#68a8bf');g.addColorStop(1,'#153548');
    ctx.beginPath();ctx.arc(head.x,head.y,13,0,Math.PI*2);ctx.fillStyle='#e8e9e2';ctx.fill();ctx.beginPath();ctx.arc(head.x+1,head.y,9,0,Math.PI*2);ctx.fillStyle=g;ctx.fill();ctx.strokeStyle='#64747c';ctx.stroke();
    const direction=projection(p.x+Math.sin(p.facing)*.65,p.z+Math.cos(p.facing)*.65,.1+p.jump);
    ctx.beginPath();ctx.moveTo(shadow.x,shadow.y-4);ctx.lineTo(direction.x,direction.y);ctx.strokeStyle='rgba(87,220,245,.42)';ctx.lineWidth=2;ctx.stroke();
  }
  function draw(){
    const w=canvas.clientWidth,h=canvas.clientHeight;
    ctx.clearRect(0,0,w,h);drawBackground(w,h);drawGround(w,h);drawLandingPad();
    for(const rock of rocks)drawRock(rock);
    const nearest=nearestFacility();
    const drawables=[...facilities.map(f=>({depth:f.x+f.z,draw:()=>{
      switch(f.type){case'habitat':drawHabitat(f);break;case'comms':drawComms(f);break;case'solar':drawSolar(f);break;case'life':drawLifeSupport(f);break;case'greenhouse':drawGreenhouse(f);break;case'rover':drawRover(f);break;}
      drawBeacon(f,nearest===f);
    }})),{depth:state.player.x+state.player.z,draw:drawAstronaut}].sort((a,b)=>a.depth-b.depth);
    for(const item of drawables)item.draw();
    const dust=ctx.createLinearGradient(0,h*.55,0,h);dust.addColorStop(0,'rgba(226,219,204,0)');dust.addColorStop(1,'rgba(223,213,193,.13)');ctx.fillStyle=dust;ctx.fillRect(0,h*.55,w,h*.45);
  }
  function distance(a,b){return Math.hypot(a.x-b.x,a.z-b.z);}
  function nearestFacility(){
    let nearest=null,dist=Infinity;
    for(const f of facilities){const d=distance(state.player,f);if(d<=f.range&&d<dist){nearest=f;dist=d;}}
    return nearest;
  }
  function showToast(text){
    ui.toast.textContent=text;ui.toast.classList.add('show');
    clearTimeout(state.toastTimer);state.toastTimer=setTimeout(()=>ui.toast.classList.remove('show'),3400);
  }
  function setOverlay(modal){
    ui.overlay.classList.remove('hidden');
    for(const node of [ui.intro,ui.briefing,ui.tutorial,ui.pause,ui.report])node.classList.add('hidden');
    if(modal)modal.classList.remove('hidden');
  }
  function hideOverlay(){ui.overlay.classList.add('hidden');}
  function startMission(){
    if(state.ended)return;
    state.running=true;state.paused=false;state.lastFrame=performance.now();hideOverlay();
    showToast('Mission clock started. Proceed to the communications station.');
  }
  const tutorialSteps=[
    {title:'Move around the outpost.',copy:'Use the keys to explore the landing site. Take your time; there is no rush.',keys:'W A S D  MOVE  ·  SHIFT  SPRINT  ·  SPACE  HOP'},
    {title:'Walk up to a facility.',copy:'Visit the communications station, solar array, and life-support systems. When a facility is close, press E or use its on-screen button.',keys:'W A S D  MOVE  ·  E  INTERACT  ·  RIGHT-DRAG  LOOK'},
    {title:'Watch your resources.',copy:'Check oxygen, water, food, and power at the top of the screen. Follow the mission objectives and choose a response when an alert appears.',keys:'OBJECTIVES  ·  RESOURCE BAR  ·  CHOOSE A RESPONSE'}
  ];
  let tutorialStep=0;
  let tutorialResumeMission=false;
  function beginTutorial(){
    tutorialResumeMission=state.running&&!state.paused;
    if(tutorialResumeMission)state.paused=true;
    showTutorialStep(0);
  }
  function showTutorialStep(index){
    tutorialStep=Math.max(0,Math.min(tutorialSteps.length-1,index));
    const step=tutorialSteps[tutorialStep];
    $('tutorialTitle').textContent=step.title;$('tutorialCopy').textContent=step.copy;$('tutorialKeys').textContent=step.keys;
    $('tutorialCounter').textContent=`STEP ${tutorialStep+1} OF ${tutorialSteps.length}`;
    $('tutorialPrevious').disabled=tutorialStep===0;
    $('tutorialNext').innerHTML=tutorialStep===tutorialSteps.length-1?'I’M READY — START MISSION <span>→</span>':'NEXT <span>→</span>';
    setOverlay(ui.tutorial);
  }
  function recordGuide(status){try{localStorage.setItem('astra_tutorial_status',status);}catch(_){}}
  function finishTutorial(status){
    recordGuide(status);
    if(tutorialResumeMission){tutorialResumeMission=false;state.paused=false;hideOverlay();}
    else startMission();
  }
  function skipGuide(){finishTutorial('skipped');}
  function advanceTutorial(){if(tutorialStep<tutorialSteps.length-1)showTutorialStep(tutorialStep+1);else finishTutorial('completed');}
  try{if(localStorage.getItem('astra_tutorial_status'))$('returningNote').textContent='Welcome back — choose the guide or jump straight in.';}catch(_){}
  function restart(){window.location.reload();}
  function complete(index){state.done[index]=true;updateUI();}
  function interact(f=state.currentFacility){
    if(!state.running||state.paused||state.eventOpen||!f)return;
    switch(f.type){
      case'comms':state.online.comms=true;complete(0);showToast('Comms link established. Mission Control is receiving your signal.');break;
      case'solar':state.online.solar=true;state.resources.power=Math.min(100,state.resources.power+8);complete(1);showToast('Solar array online. The outpost battery is charging.');break;
      case'life':if(!state.online.life){state.online.life=true;state.resources.oxygen=Math.min(100,state.resources.oxygen+4);state.resources.water=Math.min(100,state.resources.water+4);}complete(2);showToast('Life-support loop checked. Reserves are within safe game limits.');break;
      case'habitat':state.resources.habitat=Math.min(100,state.resources.habitat+3);showToast(`Habitat seal inspection complete. Condition ${Math.round(state.resources.habitat)}%.`);break;
      case'greenhouse':if(state.resources.power<8){showToast('Insufficient power for greenhouse grow lights. Restore power first.');break;}state.resources.power=Math.max(0,state.resources.power-5);state.resources.food=Math.min(100,state.resources.food+12);showToast('Greenhouse harvest logged: food +12, power −5.');break;
      case'rover':if(!state.eventDone){showToast('Stabilize the outpost before starting the rover sample run.');break;}if(!state.sample){state.sample=true;state.resources.power=Math.max(0,state.resources.power-6);complete(4);showToast('Virtual regolith sample secured. Rover telemetry uploaded.');finish(true);}break;
    }
    updateUI();
  }
  function openEvent(){
    state.eventOpen=true;state.paused=true;state.resources.power=Math.max(0,state.resources.power-14);
    ui.event.classList.add('visible');showToast('Array output has dropped. Review the mission alert.');updateUI();
  }
  function resolveEvent(shed){
    if(!state.eventOpen)return;
    state.eventOpen=false;state.eventDone=true;state.paused=false;state.safeChoice=shed;ui.event.classList.remove('visible');
    if(shed){state.resources.power=Math.min(100,state.resources.power+14);state.resources.food=Math.max(0,state.resources.food-4);showToast('Good call: shed a nonessential load to protect life support. Food production briefly slows.');}
    else{state.resources.power=Math.max(0,state.resources.power-18);state.resources.habitat=Math.max(0,state.resources.habitat-12);showToast('Full restart used reserve power. Critical loads should be prioritized in this simulation.');}
    complete(3);updateUI();
  }
  function finish(success){
    state.ended=true;state.running=false;
    const r=state.resources,score=Math.max(0,Math.min(1000, state.done.filter(Boolean).length*120+Math.round(r.habitat*1.6+((r.oxygen+r.water+r.food+r.power)/4)*1.2)+(state.safeChoice?80:20)));
    $('reportTitle').textContent=success?'Mission complete':'Mission ended early';
    $('reportDescription').textContent=success?'You brought the outpost online, responded to the alert, and secured a virtual sample.':'Critical reserves were exhausted before the mission could be completed.';
    $('scoreValue').textContent=String(score).padStart(3,'0');$('reportHabitat').textContent=`${Math.round(r.habitat)}%`;$('crewStatus').textContent=success?'SAFE':'CHECK';
    $('learningText').textContent=state.safeChoice?'Outposts balance limited power across critical and nonessential systems. Protecting life support can mean temporarily reducing another system’s output.':'Power decisions affect connected outpost systems. Monitoring the resource dashboard helps the crew respond before reserves become critical.';
    setOverlay(ui.report);
  }
  function updateObjectives(){
    const list=$('objectives');list.innerHTML='';let count=0;
    objectives.forEach((text,i)=>{if(state.done[i])count++;const li=document.createElement('li');if(state.done[i])li.className='done';li.innerHTML=`<span class="objective-mark">${state.done[i]?'✓':'○'}</span><span>${text}</span>`;list.appendChild(li);});
    $('progressLabel').textContent=`${count} / ${objectives.length}`;$('progressMeter').style.width=`${count/objectives.length*100}%`;
  }
  function updateResource(name){
    const value=state.resources[name],pct=Math.max(0,Math.min(100,value));
    $(`${name}Value`).textContent=`${Math.round(pct)}%`;$(`${name}Meter`).style.width=`${pct}%`;
    const color=pct<20?'var(--red)':pct<40?'var(--amber)':'var(--cyan)';$(`${name}Meter`).style.background=color;
    $(`${name}Value`).style.color=pct<20?'var(--red)':pct<40?'var(--amber)':'var(--text)';
  }
  function updateUI(){
    updateObjectives();for(const name of ['oxygen','water','food','power'])updateResource(name);$('habitatValue').textContent=`${Math.round(state.resources.habitat)}%`;
    $('clock').textContent=`${String(Math.floor(state.elapsed/60)).padStart(2,'0')}:${String(Math.floor(state.elapsed%60)).padStart(2,'0')}`;
    const critical=state.resources.oxygen<25||state.resources.water<25||state.resources.power<18||state.resources.habitat<60;
    $('baseStatus').textContent=critical?'ATTENTION':state.online.comms&&state.online.solar?'NOMINAL':state.running?'INITIALIZING':'STANDBY';
    $('baseStatus').style.color=critical?'var(--amber)':state.running?'var(--mint)':'var(--muted)';
    $('commsStatus').textContent=state.online.comms?'ONLINE':'STANDBY';$('commsStatus').style.color=state.online.comms?'var(--mint)':'var(--amber)';
    const f=nearestFacility();if((f?.id||null)!==(state.currentFacility?.id||null))state.facilityDismissed=null;state.currentFacility=f;
    if(f&&state.running&&!state.paused){ui.interactHint.classList.add('visible');ui.interactName.textContent=f.title.toUpperCase();ui.interactRange.textContent='IN RANGE';ui.facility.classList.toggle('visible',state.facilityDismissed!==f.id);ui.facilityTitle.textContent=f.title;ui.facilityDescription.textContent=f.description;$('siteName').textContent=f.title.toUpperCase();}
    else{ui.interactHint.classList.remove('visible');ui.facility.classList.remove('visible');$('siteName').textContent='LANDING ZONE';}
    if(!state.ended&&(state.resources.oxygen<=0||state.resources.water<=0||state.resources.habitat<=0))finish(false);
  }
  function move(dt){
    if(!state.running||state.paused||state.ended)return;
    if(state.player.jump>0||state.player.jumpV>0){state.player.jumpV-=1.62*dt;state.player.jump+=state.player.jumpV*dt;if(state.player.jump<=0){state.player.jump=0;state.player.jumpV=0;}}
    let x=0,z=0;if(state.held.has('w'))z+=1;if(state.held.has('s'))z-=1;if(state.held.has('a'))x-=1;if(state.held.has('d'))x+=1;
    const mag=Math.hypot(x,z);if(mag){x/=mag;z/=mag;const sprint=state.held.has('shift')?1.55:1;const speed=5.1*sprint;
      const a=state.camera.angle;const wx=x*Math.cos(a)+z*Math.sin(a),wz=-x*Math.sin(a)+z*Math.cos(a);
      state.player.x=Math.max(-38,Math.min(38,state.player.x+wx*speed*dt));state.player.z=Math.max(-38,Math.min(38,state.player.z+wz*speed*dt));state.player.facing=Math.atan2(wx,wz);
    }
  }
  function tick(dt){
    if(!state.running||state.paused||state.ended)return;
    state.elapsed+=dt;state.tick+=dt;
    if(state.tick>=10){state.tick-=10;const r=state.resources;r.oxygen=Math.max(0,r.oxygen-1.4);r.water=Math.max(0,r.water-1.2);r.food=Math.max(0,r.food-.8);r.power=Math.max(0,r.power+(state.online.solar?1.4:-2.2));if(r.oxygen<25||r.water<25||r.power<18)r.habitat=Math.max(0,r.habitat-2.5);}
    if(!state.eventDone&&state.online.comms&&state.online.solar&&state.elapsed>=28)openEvent();
  }
  function loop(now){
    const dt=state.lastFrame?Math.min(.05,(now-state.lastFrame)/1000):0;state.lastFrame=now;
    move(dt);tick(dt);draw();state.uiTick+=dt;if(state.uiTick>=.15){state.uiTick=0;updateUI();}requestAnimationFrame(loop);
  }

  const preventKeys=new Set([' ','ArrowUp','ArrowDown','ArrowLeft','ArrowRight']);
  window.addEventListener('keydown',e=>{
    const key=e.key.toLowerCase();if(preventKeys.has(e.key))e.preventDefault();
    if(key==='escape'){
      if(state.eventOpen)return;
      if(state.running&&!state.ended){state.paused=!state.paused;if(state.paused)setOverlay(ui.pause);else hideOverlay();}
      return;
    }
    if(key==='e'&&!e.repeat)interact();
    if(e.code==='Space'&&state.running&&!state.paused&&!state.ended&&state.player.jump===0)state.player.jumpV=Math.sqrt(2*1.62*1.4);
    if(['w','a','s','d','shift'].includes(key))state.held.add(key);
  });
  window.addEventListener('keyup',e=>state.held.delete(e.key.toLowerCase()));
  window.addEventListener('blur',()=>state.held.clear());
  canvas.addEventListener('contextmenu',e=>e.preventDefault());
  canvas.addEventListener('pointerdown',e=>{if(e.button===2){state.pointer={x:e.clientX,y:e.clientY,id:e.pointerId};canvas.setPointerCapture(e.pointerId);}});
  canvas.addEventListener('pointermove',e=>{if(!state.pointer||state.pointer.id!==e.pointerId)return;const dx=e.clientX-state.pointer.x;state.pointer.x=e.clientX;state.pointer.y=e.clientY;state.camera.angle+=dx*.006;});
  canvas.addEventListener('pointerup',e=>{if(state.pointer?.id===e.pointerId)state.pointer=null;});

  $('skipGuideButton').addEventListener('click',skipGuide);
  $('briefingStart').addEventListener('click',startMission);
  $('trainingButton').addEventListener('click',beginTutorial);
  $('replayGuideButton').addEventListener('click',beginTutorial);
  $('readBriefingButton').addEventListener('click',()=>setOverlay(ui.briefing));
  $('tutorialPrevious').addEventListener('click',()=>showTutorialStep(tutorialStep-1));
  $('tutorialNext').addEventListener('click',advanceTutorial);
  $('tutorialSkip').addEventListener('click',skipGuide);
  $('briefingButton').addEventListener('click',()=>setOverlay(ui.briefing));
  $('closeBriefing').addEventListener('click',()=>state.running?hideOverlay():setOverlay(ui.intro));
  $('pauseButton').addEventListener('click',()=>{if(!state.running||state.ended||state.eventOpen)return;state.paused=!state.paused;if(state.paused)setOverlay(ui.pause);else hideOverlay();});
  $('resumeButton').addEventListener('click',()=>{state.paused=false;hideOverlay();});
  $('restartButton').addEventListener('click',restart);
  $('againButton').addEventListener('click',restart);
  $('shedLoad').addEventListener('click',()=>resolveEvent(true));
  $('restartSystems').addEventListener('click',()=>resolveEvent(false));
  $('facilityAction').addEventListener('click',()=>interact());
  $('closeFacility').addEventListener('click',()=>{state.facilityDismissed=state.currentFacility?.id||null;ui.facility.classList.remove('visible');});
  document.addEventListener('visibilitychange',()=>{if(document.hidden)state.held.clear();});
  updateUI();requestAnimationFrame(loop);
})();
