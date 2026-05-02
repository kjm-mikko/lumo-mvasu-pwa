/* global React, Topbar, Tabbar */
/* Lisänäkymät — perustuvat Model.xafml-tiedostosta löytyneeseen navigaatioon.
 * Toteuttavat: Liidit, Lumo Verkkokauppa, Saapuneet irtisanomiset, Tarjoukset, Allekirjoitettavat,
 * Remontit, Henkilön detail. Käyttävät kit.css-luokkia + muutamia paikallisia lisäsääntöjä.
 */
const { useState: useStateExt } = React;

// Pieni inline-ikoni paikallisesti (ei tartte importtailla)
const Ix = ({ d, size = 18, vb = '0 0 24 24', children, ...rest }) => (
  <svg width={size} height={size} viewBox={vb} fill="none" stroke="currentColor"
       strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...rest}>
    {d ? <path d={d}/> : children}
  </svg>
);
const IxBack = (p) => <Ix {...p}><path d="m15 6-6 6 6 6"/></Ix>;
const IxSearch = (p) => <Ix {...p}><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></Ix>;
const IxFilter = (p) => <Ix {...p}><path d="M22 3H2l8 9.46V19l4 2v-8.54L22 3z"/></Ix>;
const IxPhone = (p) => <Ix {...p}><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.86 19.86 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.86 19.86 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.37 1.9.72 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.91.35 1.85.59 2.81.72A2 2 0 0 1 22 16.92Z"/></Ix>;
const IxKebab = (p) => <Ix {...p}><circle cx="12" cy="5" r="1.5" fill="currentColor"/><circle cx="12" cy="12" r="1.5" fill="currentColor"/><circle cx="12" cy="19" r="1.5" fill="currentColor"/></Ix>;
const IxFlame = (p) => <Ix {...p}><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"/></Ix>;
const IxChev = (p) => <Ix {...p}><path d="m9 6 6 6-6 6"/></Ix>;
const IxPlus = (p) => <Ix {...p}><path d="M12 5v14M5 12h14"/></Ix>;
const IxSign = (p) => <Ix {...p}><path d="M3 17l6-6 4 4 8-8"/><path d="M14 7h7v7"/></Ix>;
const IxWarn = (p) => <Ix {...p}><path d="M12 2 1 21h22L12 2z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></Ix>;

const ExtraTopbar = (props) => {
  // Käyttää sitä Topbaria joka on jo määritelty screens.jsx:ssä
  // (on globaalisti saatavilla saman Babel-koonnin ansiosta)
  return <Topbar {...props}/>;
};

// ===================== LIIDIT =====================
// Lähde: ASMA → Liidit · ViewId Hakemus_Saapuneet_ListView_DetailViev
// Hakemustyyppi.HakemustyyppiId = 4 (Liidi). Keltaiset rivit = uudet.
const LiiditScreen = ({ onTab, onBack }) => {
  const [filt, setFilt] = useStateExt('uudet');
  const liidit = [
    { hn: '1 300 884', nimi: 'Enckell, Tiia', kunta: 'Lappeenranta', tyyppi: 'Kaksio', vuokra: 555, tila: 'Uusi', uusi: true, kontaktoitu: null },
    { hn: '1 300 712', nimi: 'Koivuranta, Kaija', kunta: 'Oulu', tyyppi: 'Kaksio', vuokra: 800, tila: 'Käsittelyssä', uusi: false, kontaktoitu: 'NiemiMik · 6.4.' },
    { hn: '1 300 698', nimi: 'Koivumikko, Kaija', kunta: 'Oulu, Lampi…', tyyppi: 'Neliö', vuokra: 1200, tila: 'Tarjottu', uusi: false, kontaktoitu: 'koivuksaija · 5.4.', flag: 'Ei vastattu' },
    { hn: '1 300 502', nimi: 'Testaaja, Maaliskuu', kunta: 'Mäntsälä', tyyppi: 'Kolmio', vuokra: 950, tila: 'Uusi', uusi: true, kontaktoitu: null },
    { hn: '1 300 401', nimi: 'Aurinkoinen, Tuutikki', kunta: 'Espoo', tyyppi: 'Kolmio', vuokra: 1100, tila: 'Uusi', uusi: true, kontaktoitu: null },
    { hn: '1 300 297', nimi: 'Helmikuu, Helmiä', kunta: 'Espoo', tyyppi: 'Kaksio', vuokra: 750, tila: 'Käsittelyssä', uusi: false, kontaktoitu: 'NiemiMik · 4.4.' },
    { hn: '1 300 188', nimi: 'Jätkä, Saari', kunta: 'Helsinki', tyyppi: 'Yksiö', vuokra: 700, tila: 'Käsittelyssä', uusi: false, kontaktoitu: 'koivuksaija · 4.4.' },
  ];

  return (
    <div className="ac-app">
      <ExtraTopbar
        leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
        eyebrow="ASMA"
        title="Liidit"
        trailing={<><button className="icon-btn"><IxSearch size={20}/></button><button className="icon-btn"><IxFilter size={20}/></button></>}
      />
      <div className="ac-segment">
        <button className={'seg-btn' + (filt === 'uudet' ? ' active' : '')} onClick={() => setFilt('uudet')}>Uudet<span className="seg-count">3</span></button>
        <button className={'seg-btn' + (filt === 'kasittelyssa' ? ' active' : '')} onClick={() => setFilt('kasittelyssa')}>Käsittelyssä<span className="seg-count">3</span></button>
        <button className={'seg-btn' + (filt === 'tarjotut' ? ' active' : '')} onClick={() => setFilt('tarjotut')}>Tarjotut<span className="seg-count">1</span></button>
        <button className={'seg-btn' + (filt === 'kaikki' ? ' active' : '')} onClick={() => setFilt('kaikki')}>Kaikki</button>
      </div>
      <div className="ac-body">
        {liidit.map((l) => (
          <div key={l.hn} className={'liidi-card' + (l.uusi ? ' uusi' : '')}>
            <div className="lc-head">
              <div className="lc-name">{l.nimi}</div>
              <button className="kebab-btn"><IxKebab size={18}/></button>
            </div>
            <div className="lc-meta">{l.kunta} · {l.tyyppi} · max {l.vuokra} €/kk</div>
            <div className="lc-foot">
              <span className={'tila-pill ' + (l.uusi ? 'uusi' : (l.tila === 'Tarjottu' ? 'info' : 'progress'))}>
                {l.uusi ? '🆕 ' : ''}{l.tila}
              </span>
              {l.flag ? <span className="tila-pill warn">{l.flag}</span> : null}
              <span className="lc-hn">HN {l.hn}</span>
            </div>
            {l.kontaktoitu ? <div className="lc-kontakti">Viim. kontakti: {l.kontaktoitu}</div> : <div className="lc-kontakti emp">Ei vielä kontaktoitu</div>}
          </div>
        ))}
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== LUMO VERKKOKAUPPA =====================
// Lähde: ASMA → Lumo Verkkokauppa · DirectRental_ListView_Myyntineuvottelija_DetailView
// Direct Rental: asiakas teki päätöksen verkossa, mVasussa käsittely.
const VerkkokauppaScreen = ({ onTab, onBack }) => {
  const tilaukset = [
    { sn: 203227, osoite: 'Ellipsikuja 2 D 8', kunta: '02210 Espoo', vuokr: 'Ikonen, Ansa', tila: 'Confirmed', voimaan: '1.3.2026', sopTila: 'Toistaiseksi', uusi: true, ohitettu: true },
    { sn: 203195, osoite: 'Ellipsikuja 2 H 17', kunta: '02210 Espoo', vuokr: 'Testaaja, Maaliskuu', tila: 'Käsittelyssä', voimaan: '15.3.2026', sopTila: 'Toistaiseksi', uusi: true },
    { sn: 203102, osoite: 'Ellipsikuja 2 E 11', kunta: '02210 Espoo', vuokr: 'Watson, Juan', tila: 'Confirmed', voimaan: '1.4.2026', sopTila: 'Toistaiseksi' },
    { sn: 202987, osoite: 'Ellipsikuja 2 B 3', kunta: '02210 Espoo', vuokr: 'Gutierrez, Henry', tila: 'Confirmed', voimaan: '1.4.2026', sopTila: 'Toistaiseksi' },
    { sn: 202841, osoite: 'Ellipsikuja 2 E 10', kunta: '02210 Espoo', vuokr: 'Russell, Armando', tila: 'Confirmed', voimaan: '1.5.2026', sopTila: 'Määräaik. 1v' },
    { sn: 202774, osoite: 'Ellipsikuja 2 E 16', kunta: '02210 Espoo', vuokr: 'Thomas, Barry', tila: 'Käsittelyssä', voimaan: '1.5.2026', sopTila: 'Toistaiseksi', uusi: true },
  ];
  return (
    <div className="ac-app">
      <ExtraTopbar
        leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
        eyebrow="ASMA"
        title="Lumo Verkkokauppa"
        trailing={<><button className="icon-btn"><IxSearch size={20}/></button><button className="icon-btn"><IxFilter size={20}/></button></>}
      />
      <div className="ac-body">
        <div className="info-banner">
          <IxFlame size={16}/>
          <span><strong>3 uutta</strong> verkkokaupasta saapunutta vuokrausta odottaa käsittelyä</span>
        </div>
        {tilaukset.map((t) => (
          <div key={t.sn} className={'vk-card' + (t.uusi ? ' uusi' : '')}>
            <div className="vk-head">
              <div className="vk-osoite">{t.osoite}</div>
              <button className="kebab-btn"><IxKebab size={18}/></button>
            </div>
            <div className="vk-kunta">{t.kunta}</div>
            <div className="vk-row"><span className="vk-label">Vuokralainen</span><span className="vk-val">{t.vuokr}</span></div>
            <div className="vk-row"><span className="vk-label">Voimaan</span><span className="vk-val">{t.voimaan} · {t.sopTila}</span></div>
            <div className="vk-foot">
              <span className={'tila-pill ' + (t.tila === 'Confirmed' ? 'done' : 'progress')}>{t.tila}</span>
              {t.ohitettu ? <span className="tila-pill warn">Ohitti TK</span> : null}
              <span className="vk-sn">#{t.sn}</span>
            </div>
          </div>
        ))}
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== SAAPUNEET IRTISANOMISET =====================
// Lähde: ASMA → SaapuneetIrtisanomiset_ListView_DetailView
const IrtisanomisetScreen = ({ onTab, onBack }) => {
  const irti = [
    { osoite: 'Lumokuja 3 A 12', kunta: '00400 Helsinki', vuokr: 'Mäkelä, Pia', saapui: '8.4.2026', voimaan: '31.5.2026', syy: 'Muutto pois Suomesta', uusi: true },
    { osoite: 'Sammonkatu 7 B 4', kunta: '33540 Tampere', vuokr: 'Lehto, Ari', saapui: '7.4.2026', voimaan: '30.4.2026', syy: 'Asunnonvaihto', uusi: true, kiire: true },
    { osoite: 'Mannerheimintie 80 C 2', kunta: '00270 Helsinki', vuokr: 'Korhonen, Eveliina', saapui: '5.4.2026', voimaan: '31.5.2026', syy: 'Omistusasunto' },
    { osoite: 'Hämeentie 14 A 8', kunta: '00530 Helsinki', vuokr: 'Salo, Tomi', saapui: '3.4.2026', voimaan: '30.6.2026', syy: '—' },
    { osoite: 'Pyynikintie 3 B 2', kunta: '33230 Tampere', vuokr: 'Halonen, Marjo', saapui: '1.4.2026', voimaan: '30.4.2026', syy: 'Työn perässä', kiire: true },
  ];
  return (
    <div className="ac-app">
      <ExtraTopbar
        leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
        eyebrow="ASMA"
        title="Saapuneet irtisanomiset"
        trailing={<><button className="icon-btn"><IxSearch size={20}/></button><button className="icon-btn"><IxFilter size={20}/></button></>}
      />
      <div className="ac-body">
        {irti.map((it, i) => (
          <div key={i} className={'irti-card' + (it.uusi ? ' uusi' : '')}>
            <div className="irti-head">
              <div>
                <div className="irti-osoite">{it.osoite}</div>
                <div className="irti-kunta">{it.kunta}</div>
              </div>
              {it.kiire ? <span className="tila-pill warn"><IxWarn size={11}/> Kiire</span> : null}
            </div>
            <div className="irti-vuokr">{it.vuokr}</div>
            <div className="irti-syy">"{it.syy}"</div>
            <div className="irti-foot">
              <div className="irti-times">
                <div><span className="irti-tlabel">Saapui</span><span className="irti-tval">{it.saapui}</span></div>
                <div><span className="irti-tlabel">Päättyy</span><span className="irti-tval">{it.voimaan}</span></div>
              </div>
              <button className="a-btn primary" style={{minHeight: 32}}>Käsittele</button>
            </div>
          </div>
        ))}
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== TARJOUKSET =====================
// Lähde: Päätaso → Tarjoukset · SopimusVaraus_ListView_DetailView
const TarjouksetScreen = ({ onTab, onBack }) => {
  const tarj = [
    { huoneisto: 'Annankatu 5 A 2', kunta: '00100 Helsinki', tyyppi: '2h+kk · 47 m²', vuokr: 'Aalto, Eero', vuokra: 920, paattyy: 'Tänään 17:00', kiire: true, tila: 'Avoin' },
    { huoneisto: 'Iso Roobertinkatu 18', kunta: '00120 Helsinki', tyyppi: '3h+k · 76 m²', vuokr: 'Kallio, Janina', vuokra: 1480, paattyy: 'Huomenna 12:00', tila: 'Avoin' },
    { huoneisto: 'Mäkelänkatu 56 B', kunta: '00510 Helsinki', tyyppi: '1h+kk · 28 m²', vuokr: 'Salminen, Olli', vuokra: 720, paattyy: '10.4. 16:00', tila: 'Avoin' },
    { huoneisto: 'Pohjolankatu 4 A', kunta: '00610 Helsinki', tyyppi: '2h+k · 54 m²', vuokr: 'Niemi, Kerttu', vuokra: 1050, paattyy: '11.4. 12:00', tila: 'Hyväksytty' },
    { huoneisto: 'Vuorikatu 12 C', kunta: '00100 Helsinki', tyyppi: '3h+k · 81 m²', vuokr: 'Heikkinen, Ville', vuokra: 1620, paattyy: '12.4. 14:00', tila: 'Hyväksytty' },
    { huoneisto: 'Tehtaankatu 8 B', kunta: '00140 Helsinki', tyyppi: '2h+kk · 41 m²', vuokr: 'Mattila, Saana', vuokra: 880, paattyy: '13.4. 10:00', tila: 'Avoin' },
  ];
  return (
    <div className="ac-app">
      <ExtraTopbar
        leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
        title="Tarjoukset"
        trailing={<><button className="icon-btn"><IxSearch size={20}/></button><button className="icon-btn"><IxPlus size={20}/></button></>}
      />
      <div className="ac-body">
        {tarj.map((t, i) => (
          <div key={i} className={'tarj-card' + (t.kiire ? ' kiire' : '')}>
            <div className="tarj-head">
              <div className="tarj-osoite">{t.huoneisto}</div>
              <span className={'tila-pill ' + (t.tila === 'Hyväksytty' ? 'done' : 'progress')}>{t.tila}</span>
            </div>
            <div className="tarj-meta">{t.tyyppi} · {t.kunta}</div>
            <div className="tarj-row">
              <div>
                <div className="tarj-label">Hakija</div>
                <div className="tarj-val">{t.vuokr}</div>
              </div>
              <div>
                <div className="tarj-label">Vuokra</div>
                <div className="tarj-val">{t.vuokra} €/kk</div>
              </div>
            </div>
            <div className={'tarj-deadline' + (t.kiire ? ' kiire' : '')}>
              {t.kiire ? <IxFlame size={14}/> : null}
              <span>Päättyy: <strong>{t.paattyy}</strong></span>
            </div>
          </div>
        ))}
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== ALLEKIRJOITETTAVAT =====================
// Lähde: ASMA → Allekirjoitettavat · Sopimus_Allekirjoitettavat_ListView_Detailview
const AllekirjoitettavatScreen = ({ onTab, onBack }) => {
  const sop = [
    { tyyppi: 'Vuokrasopimus', huoneisto: 'Annankatu 5 A 2', vuokr: 'Aalto, Eero', alkaa: '1.5.2026', menetelma: 'Sähköinen · DocuSign', tila: 'Odottaa hakijaa', edist: 1, max: 2 },
    { tyyppi: 'Vuokrasopimus', huoneisto: 'Lumokuja 3 A 12', vuokr: 'Korhonen, Eveliina', alkaa: '1.6.2026', menetelma: 'Paperinen', tila: 'Odottaa kirjoittajaa', edist: 0, max: 2 },
    { tyyppi: 'Sopimuksen muutos', huoneisto: 'Mäkelänkatu 56 B', vuokr: 'Salminen, Olli', alkaa: 'Heti', menetelma: 'Sähköinen · DocuSign', tila: 'Lähes valmis', edist: 2, max: 2, kiire: true },
  ];
  return (
    <div className="ac-app">
      <ExtraTopbar
        leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
        eyebrow="ASMA"
        title="Allekirjoitettavat"
        trailing={<button className="icon-btn"><IxSearch size={20}/></button>}
      />
      <div className="ac-body">
        {sop.map((s, i) => (
          <div key={i} className={'allek-card' + (s.kiire ? ' kiire' : '')}>
            <div className="allek-head">
              <span className="allek-icon"><IxSign size={18}/></span>
              <div className="allek-titles">
                <div className="allek-tyyppi">{s.tyyppi}</div>
                <div className="allek-osoite">{s.huoneisto}</div>
              </div>
              <button className="kebab-btn"><IxKebab size={18}/></button>
            </div>
            <div className="allek-row">
              <div><span className="allek-label">Vuokralainen</span><span className="allek-val">{s.vuokr}</span></div>
              <div><span className="allek-label">Alkaa</span><span className="allek-val">{s.alkaa}</span></div>
            </div>
            <div className="allek-progress">
              <div className="ap-bar"><div className="ap-fill" style={{width: ((s.edist / s.max) * 100) + '%'}}/></div>
              <span className="ap-label">{s.edist}/{s.max} allekirjoitusta · {s.menetelma}</span>
            </div>
            <div className="allek-foot">
              <span className={'tila-pill ' + (s.tila === 'Lähes valmis' ? 'done' : 'progress')}>{s.tila}</span>
              <button className="a-btn primary" style={{minHeight: 32}}>Avaa</button>
            </div>
          </div>
        ))}
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== REMONTIT (KIRE) =====================
// Lähde: KIRE → Remontti · RemonttiTiedot_ListView_DetailView
const RemontitScreen = ({ onTab, onBack }) => {
  const rem = [
    { tyyppi: 'Keittiöremontti', kohde: 'Annankatu 5 A 2', tila: 'Suunnitteilla', alkaa: '15.5.2026', kesto: '4 vk', urakoitsija: 'Lumo Rakennus Oy' },
    { tyyppi: 'Kylpyhuoneen kunnostus', kohde: 'Lumokuja 3 A 12', tila: 'Käynnissä', alkaa: '1.4.2026', kesto: '3 vk', urakoitsija: 'Helsinki Remontti', edist: 60 },
    { tyyppi: 'Putkiremontti', kohde: 'Mannerheimintie 80', tila: 'Suunnitteilla', alkaa: '1.6.2026', kesto: '12 vk', urakoitsija: '—' },
    { tyyppi: 'Lattian uusinta', kohde: 'Sammonkatu 7 B 4', tila: 'Valmis', alkaa: '1.3.2026', kesto: '2 vk', urakoitsija: 'Tampere Lattia', edist: 100 },
    { tyyppi: 'Maalaus', kohde: 'Hämeentie 14 A 8', tila: 'Käynnissä', alkaa: '5.4.2026', kesto: '1 vk', urakoitsija: 'Lumo Rakennus Oy', edist: 30 },
  ];
  return (
    <div className="ac-app">
      <ExtraTopbar
        leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
        eyebrow="KIRE"
        title="Remontit"
        trailing={<button className="icon-btn"><IxPlus size={20}/></button>}
      />
      <div className="ac-body">
        {rem.map((r, i) => (
          <div key={i} className="rem-card">
            <div className="rem-head">
              <div>
                <div className="rem-tyyppi">{r.tyyppi}</div>
                <div className="rem-kohde">{r.kohde}</div>
              </div>
              <span className={'tila-pill ' + (r.tila === 'Käynnissä' ? 'progress' : (r.tila === 'Valmis' ? 'done' : 'planned'))}>{r.tila}</span>
            </div>
            <div className="rem-row">
              <div><span className="rem-label">Alkaa</span><span className="rem-val">{r.alkaa}</span></div>
              <div><span className="rem-label">Kesto</span><span className="rem-val">{r.kesto}</span></div>
              <div><span className="rem-label">Urakoitsija</span><span className="rem-val">{r.urakoitsija}</span></div>
            </div>
            {r.edist != null ? (
              <div className="rem-progress">
                <div className="rp-bar"><div className="rp-fill" style={{width: r.edist + '%'}}/></div>
                <span className="rp-label">{r.edist}%</span>
              </div>
            ) : null}
          </div>
        ))}
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== HENKILÖ DETAIL =====================
// Lähde: ASMA → Henkilöt · Henkilo_ListView_Detailview, klikkaus avaa detail
const HenkiloDetailScreen = ({ onBack }) => (
  <div className="ac-app">
    <ExtraTopbar
      leading={<button className="icon-btn" onClick={onBack}><IxBack size={20}/></button>}
      title="Henkilö"
      trailing={<button className="icon-btn"><IxKebab size={20}/></button>}
    />
    <div className="ac-body">
      <div className="hk-hero">
        <span className="hk-avatar">AE</span>
        <div className="hk-info">
          <div className="hk-name">Aalto, Eero</div>
          <div className="hk-meta">Asukas · 1980 · Asukas-NRO 1 200 412</div>
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Yhteystiedot</div>
        <button className="row-link"><IxPhone size={18}/><span className="rl-info"><span className="rl-title">+358 40 123 4567</span><span className="rl-meta">Matkapuhelin · ensisijainen</span></span><IxChev size={16}/></button>
        <button className="row-link" style={{marginTop: 6}}><Ix size={18}><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 6-10 7L2 6"/></Ix><span className="rl-info"><span className="rl-title">eero.aalto@example.com</span><span className="rl-meta">Sähköposti</span></span><IxChev size={16}/></button>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Asunto</div>
        <button className="row-link"><Ix size={18}><path d="M4 21V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v15"/></Ix><span className="rl-info"><span className="rl-title">Annankatu 5 A 2</span><span className="rl-meta">2h+kk · 47 m² · 920 €/kk</span></span><IxChev size={16}/></button>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Sopimus</div>
        <button className="row-link"><IxSign size={18}/><span className="rl-info"><span className="rl-title">Vuokrasopimus #SOP-2024-1284</span><span className="rl-meta">Voimassa 1.6.2024 →</span></span><IxChev size={16}/></button>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Hakemukset & liidit</div>
        <button className="row-link"><Ix size={18}><path d="M21 11.5V7a2 2 0 0 0-2-2h-7l-2-2H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h6"/><path d="M16 19h6M19 16v6"/></Ix><span className="rl-info"><span className="rl-title">Ei aktiivisia hakemuksia</span><span className="rl-meta">Viimeisin: 2024 (täytetty)</span></span><IxChev size={16}/></button>
      </div>
    </div>
    <div className="ac-footer-cta">
      <button className="btn-cta">Soita</button>
    </div>
  </div>
);

// Vie globaaliin scopeen
Object.assign(window, {
  LiiditScreen,
  VerkkokauppaScreen,
  IrtisanomisetScreen,
  TarjouksetScreen,
  AllekirjoitettavatScreen,
  RemontitScreen,
  HenkiloDetailScreen,
});
