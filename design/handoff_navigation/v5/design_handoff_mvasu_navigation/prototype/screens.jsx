/* global React */
const { useState } = React;

const I = ({ d, size = 22, vb = '0 0 24 24', children, ...rest }) => (
  <svg width={size} height={size} viewBox={vb} fill="none" stroke="currentColor"
       strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...rest}>
    {d ? <path d={d}/> : children}
  </svg>
);

// Bottom-tab icons
const ICheckbox = (p) => <I {...p}><polyline points="9 11 12 14 22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></I>;
const IList = (p) => <I {...p}><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></I>;
const IUsers = (p) => <I {...p}><circle cx="9" cy="8" r="3.5"/><circle cx="17.5" cy="9.5" r="2.5"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><path d="M15 17c.5-1.8 2.4-3 4.5-3 1.2 0 2.5.5 2.5 3"/></I>;
const IBuilding = (p) => <I {...p}><path d="M4 21V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v15"/><path d="M16 9h2a2 2 0 0 1 2 2v10"/><path d="M8 8h.01M8 12h.01M8 16h.01M12 8h.01M12 12h.01M12 16h.01M16 13h.01M16 17h.01"/></I>;
const IMore = (p) => <I {...p}><circle cx="12" cy="12" r="1.5" fill="currentColor"/><circle cx="5" cy="12" r="1.5" fill="currentColor"/><circle cx="19" cy="12" r="1.5" fill="currentColor"/></I>;
const IKebab = (p) => <I {...p}><circle cx="12" cy="5" r="1.5" fill="currentColor"/><circle cx="12" cy="12" r="1.5" fill="currentColor"/><circle cx="12" cy="19" r="1.5" fill="currentColor"/></I>;
// Card icons
const ICalendar = (p) => <I {...p}><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 9h18M8 3v4M16 3v4"/></I>;
const IPhone = (p) => <I {...p}><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.86 19.86 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.86 19.86 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.37 1.9.72 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.91.35 1.85.59 2.81.72A2 2 0 0 1 22 16.92Z"/></I>;
const ICamera = (p) => <I {...p}><path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3z"/><circle cx="12" cy="13" r="3.5"/></I>;
const IWrench = (p) => <I {...p}><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94L8.49 19.5a2.12 2.12 0 1 1-3-3l6.03-6.03a6 6 0 0 1 7.94-7.94l-3.76 3.77z"/></I>;
const IClock = (p) => <I {...p}><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></I>;
const ISearch = (p) => <I {...p}><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></I>;
const IFilter = (p) => <I {...p}><path d="M22 3H2l8 9.46V19l4 2v-8.54L22 3z"/></I>;
const IChev = (p) => <I {...p}><path d="m9 6 6 6-6 6"/></I>;
const IPlus = (p) => <I {...p}><path d="M12 5v14M5 12h14"/></I>;
const IBell = (p) => <I {...p}><path d="M6 19V11a6 6 0 1 1 12 0v8"/><path d="M3 19h18M10 22h4"/></I>;
const IBack = (p) => <I {...p}><path d="m15 6-6 6 6 6"/></I>;
const IFlame = (p) => <I {...p}><path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z"/></I>;
const IBolt = (p) => <I {...p}><path d="M13 2 3 14h9l-1 8 10-12h-9l1-8z"/></I>;
const IKey = (p) => <I {...p}><circle cx="7.5" cy="15.5" r="4.5"/><path d="m11 12 9-9M16 7l3 3"/></I>;
const ICalc = (p) => <I {...p}><rect x="4" y="2" width="16" height="20" rx="2"/><path d="M8 6h8M8 10h.01M12 10h.01M16 10h.01M8 14h.01M12 14h.01M16 14h.01M8 18h.01M12 18h.01M16 18h.01"/></I>;
const ILink = (p) => <I {...p}><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></I>;
const ICheck = (p) => <I {...p}><polyline points="20 6 9 17 4 12"/></I>;
const IX = (p) => <I {...p}><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></I>;

const Tabbar = ({ active, onChange }) => {
  const tabs = [
    { id: 'tasks', label: 'Tehtävät', icon: ICheckbox },
    { id: 'tiskilista', label: 'Tiskilista', icon: IList },
    { id: 'people', label: 'Asukkaat', icon: IUsers },
    { id: 'units', label: 'Kohteet', icon: IBuilding },
    { id: 'more', label: 'Lisää', icon: IMore },
  ];
  return (
    <div className="ac-tabbar">
      {tabs.map(t => {
        const Ic = t.icon;
        return (
          <button key={t.id} className={'ac-tab' + (active === t.id ? ' active' : '')} onClick={() => onChange?.(t.id)}>
            <Ic size={22}/>
            <span>{t.label}</span>
          </button>
        );
      })}
    </div>
  );
};

const Topbar = ({ title, leading, trailing, eyebrow }) => (
  <div className="ac-topbar">
    {leading || <span style={{width: 32}}/>}
    <div className="title-block">
      {eyebrow ? <div className="eyebrow">{eyebrow}</div> : null}
      <div className="title">{title}</div>
    </div>
    <span className="spacer"/>
    {trailing}
  </div>
);

// ===================== TEHTÄVÄT (käyttäjän omat — Tehtävä-entiteetti) =====================
const PrioPill = ({ p }) => {
  const map = { high: { label: 'High', cls: 'prio-high' }, med: { label: 'Medium', cls: 'prio-med' }, low: { label: 'Low', cls: 'prio-low' } };
  const x = map[p] || map.med;
  return <span className={'prio-pill ' + x.cls}>{x.label}</span>;
};

const StatusPill = ({ s }) => {
  const map = {
    'not-started': { label: 'Ei aloitettu', cls: 'st-notstarted' },
    'in-progress': { label: 'Käynnissä', cls: 'st-progress' },
    'done': { label: 'Valmis', cls: 'st-done' },
  };
  const x = map[s] || map['not-started'];
  return <span className={'status-pill ' + x.cls}>{x.label}</span>;
};

const TehtavaCard = ({ task, onMenu, onOpen }) => (
  <div className="tehtava-card" onClick={() => onOpen?.(task)}>
    <div className="tc-head">
      <span className="tc-type">{task.tyyppi}</span>
      <PrioPill p={task.prio}/>
      <button className="kebab-btn" onClick={(e) => { e.stopPropagation(); onMenu?.(task); }} aria-label="Toiminnot">
        <IKebab size={18}/>
      </button>
    </div>
    <div className="tc-title">{task.otsikko}</div>
    {task.kohde ? <div className="tc-kohde">{task.kohde}</div> : null}
    <div className="tc-foot">
      <StatusPill s={task.status}/>
      <span className="tc-when"><IClock size={12}/> {task.aloitus}</span>
      {task.valmiina != null ? <span className="tc-progress">{task.valmiina}%</span> : null}
    </div>
  </div>
);

const TasksScreen = ({ onTab, onCardMenu, onOpenTask }) => {
  const [filter, setFilter] = useState('omat');
  const tehtavat = [
    { id: 1, tyyppi: 'Remontin tilaus', otsikko: 'Tilaa keittiön remontti', kohde: 'Malminiityntie 16 B 94', prio: 'high', status: 'in-progress', aloitus: '7.10. 16:00', valmiina: 40 },
    { id: 2, tyyppi: 'Tarkastus', otsikko: 'Tarkista vesivahingon korjaus', kohde: 'Vänrikinkatu 2 Varasto', prio: 'high', status: 'not-started', aloitus: 'Tänään 14:00' },
    { id: 3, tyyppi: 'Hinnoittelu', otsikko: 'Päivitä vuokrahinta 2026', kohde: 'Aerolankaari 13 A 17', prio: 'med', status: 'not-started', aloitus: 'Huomenna' },
    { id: 4, tyyppi: 'Muu', otsikko: 'Soita asukkaalle palautteesta', kohde: 'Karhuntie 16 E 39', prio: 'low', status: 'in-progress', aloitus: '8.10.', valmiina: 20 },
    { id: 5, tyyppi: 'Tarkastus', otsikko: 'Sauna-osasto: pintojen kunto', kohde: 'Maauunintie 23 A 2', prio: 'med', status: 'not-started', aloitus: '10.10.' },
  ];
  return (
    <div className="ac-app">
      <Topbar
        eyebrow="Käyttäjän tehtävälista"
        title="Tehtävät"
        trailing={
          <>
            <button className="icon-btn"><ISearch size={20}/></button>
            <button className="icon-btn"><IPlus size={22}/></button>
          </>
        }
      />
      <div className="ac-segment">
        <button className={'seg-btn' + (filter === 'omat' ? ' active' : '')} onClick={() => setFilter('omat')}>Omat <span className="seg-count">5</span></button>
        <button className={'seg-btn' + (filter === 'liitetyt' ? ' active' : '')} onClick={() => setFilter('liitetyt')}>Liitetyt <span className="seg-count">2</span></button>
        <button className={'seg-btn' + (filter === 'kaikki' ? ' active' : '')} onClick={() => setFilter('kaikki')}>Kaikki</button>
      </div>
      <div className="ac-body">
        <div className="day-header">
          <span className="day">Käynnissä</span>
          <span className="count">2 tehtävää</span>
        </div>
        {tehtavat.filter(t => t.status === 'in-progress').map(t =>
          <TehtavaCard key={t.id} task={t} onMenu={onCardMenu} onOpen={onOpenTask}/>
        )}
        <div className="day-header">
          <span className="day">Ei aloitettu</span>
          <span className="count">3 tehtävää</span>
        </div>
        {tehtavat.filter(t => t.status === 'not-started').map(t =>
          <TehtavaCard key={t.id} task={t} onMenu={onCardMenu} onOpen={onOpenTask}/>
        )}
      </div>
      <Tabbar active="tasks" onChange={onTab}/>
    </div>
  );
};

// ===================== TEHTÄVÄ-DETAIL =====================
const TehtavaDetailScreen = ({ onBack }) => (
  <div className="ac-app">
    <Topbar
      title="Tehtävä"
      leading={<button className="icon-btn" onClick={onBack}><IBack size={22}/></button>}
      trailing={<button className="icon-btn"><IKebab size={20}/></button>}
    />
    <div className="ac-body">
      <div className="hero-block">
        <div className="hero-pills">
          <PrioPill p="high"/>
          <StatusPill s="in-progress"/>
          <span className="tc-type">Remontin tilaus</span>
        </div>
        <h1 className="hero-title">Tilaa keittiön remontti</h1>
        <div className="hero-sub">Aloitettu 7.10.2025 16:00 · Valmiina 40 %</div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Liittyvä kohde</div>
        <button className="row-link">
          <IBuilding size={18}/>
          <div className="rl-info">
            <div className="rl-title">Malminiityntie 16 B 94</div>
            <div className="rl-meta">3H+K · 78 m² · 01350 Vantaa</div>
          </div>
          <IChev size={16}/>
        </button>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Liitetty sopimus</div>
        <button className="row-link">
          <ICheckbox size={18}/>
          <div className="rl-info">
            <div className="rl-title">100029 / LEE PHILIP</div>
            <div className="rl-meta">Vänrikinkatu 2 Varasto</div>
          </div>
          <IChev size={16}/>
        </button>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Kuvaus</div>
        <div className="note-block">
          Muistathan käydä testaamassa tätä taskin luontia. Tuleeko muistutus perjantaina. Hanat, vetimet ja altaan vaihto.
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Liitetyt käyttäjät</div>
        <div className="chip-row">
          <span className="user-chip"><span className="avatar">KK</span> koivukaija</span>
          <span className="user-chip"><span className="avatar">PM</span> parkkmik</span>
          <span className="user-chip"><span className="avatar">VL</span> vekolasse</span>
          <button className="user-chip add"><IPlus size={14}/> Lisää</button>
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Toiminnot</div>
        <button className="row-action"><span>Merkitse käynnistyneeksi</span><IChev size={16}/></button>
        <button className="row-action"><span>Päivitä valmiusaste</span><IChev size={16}/></button>
        <button className="row-action"><span>Siirrä myöhempään (Postpone)</span><IChev size={16}/></button>
        <button className="row-action"><span>Liitä huoneisto / sopimus</span><IChev size={16}/></button>
        <button className="row-action danger"><span>Poista tehtävä</span><IChev size={16}/></button>
      </div>
    </div>
    <div className="ac-footer-cta dual">
      <button className="btn-outline">Postpone</button>
      <button className="btn-cta">Merkitse valmiiksi</button>
    </div>
  </div>
);

// ===================== TISKILISTA (vapaat asunnot) =====================
const TiskiCard = ({ apt, onMenu, onOpen }) => (
  <div className={'tiski-card' + (apt.priorityFlag ? ' flagged' : '')} onClick={() => onOpen?.(apt)}>
    <div className="tk-head">
      <div className="tk-addr">
        <div className="tk-osoite">{apt.osoite}</div>
        <div className="tk-meta">{apt.tyyppi} · {apt.m2} m² · {apt.kerros}.krs</div>
      </div>
      <button className="kebab-btn" onClick={(e) => { e.stopPropagation(); onMenu?.(apt); }} aria-label="Toiminnot">
        <IKebab size={18}/>
      </button>
    </div>
    <div className="tk-row">
      <span className="tk-pill tila">Vapaa</span>
      {apt.lumofi ? <span className="tk-pill lumofi">Lumo.fi</span> : null}
      {apt.kuvausTarve ? <span className="tk-pill needs"><ICamera size={11}/> Kuvaus</span> : null}
      {apt.tarkastus ? <span className="tk-pill needs"><ICheck size={11}/> Tarkastus</span> : null}
    </div>
    <div className="tk-foot">
      <div className="tk-money">
        <div className="tk-vuokra">{apt.vuokra} €/kk</div>
        <div className="tk-vapaa">vapaa {apt.vapautuu}</div>
      </div>
      <div className="tk-features">
        {apt.parveke ? <span title="Parveke">P</span> : null}
        {apt.sauna ? <span title="Sauna">S</span> : null}
        {apt.hissi ? <span title="Hissi">H</span> : null}
      </div>
    </div>
  </div>
);

const TiskilistaScreen = ({ onTab, onCardMenu, onOpenApt, onToolbar }) => {
  const [tab, setTab] = useState('vapaat');
  const apts = [
    { id: 1, osoite: 'Päijänteentie 4-6 C 36', tyyppi: '2H+K', m2: 47.5, kerros: '2/4', vuokra: '1 098', vapautuu: '01.04.2026', lumofi: false, kuvausTarve: true },
    { id: 2, osoite: 'Mellonkatu 12 B 21', tyyppi: '3H+K+S', m2: 72.0, kerros: '3/5', vuokra: '1 498', vapautuu: '20.04.2026', lumofi: true, sauna: true, parveke: true },
    { id: 3, osoite: 'Annankatu 5 A 2', tyyppi: 'PÄIVÄK.', m2: 88.0, kerros: '1/4', vuokra: '1 625', vapautuu: '01.02.2028', lumofi: false, priorityFlag: true, kuvausTarve: true, tarkastus: true },
    { id: 4, osoite: 'Näyttelijäntie 22 D 46', tyyppi: '1H+KK', m2: 25.5, kerros: '4/4', vuokra: '780', vapautuu: '01.04.2026', lumofi: true, hissi: true },
    { id: 5, osoite: 'Vuorengekontie 5 A 32', tyyppi: '3H+K', m2: 53.0, kerros: '2/3', vuokra: '1 130', vapautuu: '01.11.2020', lumofi: true, parveke: true },
    { id: 6, osoite: 'Roihuvuorentie 20 J 97', tyyppi: '1H+KK', m2: 22.0, kerros: '5/6', vuokra: '690', vapautuu: '01.04.2026', lumofi: true, hissi: true },
  ];
  return (
    <div className="ac-app">
      <Topbar
        title="Tiskilista"
        eyebrow="120 vapaata asuntoa"
        trailing={
          <>
            <button className="icon-btn"><ISearch size={20}/></button>
            <button className="icon-btn"><IFilter size={20}/></button>
          </>
        }
      />
      <div className="ac-segment">
        <button className={'seg-btn' + (tab === 'vapaat' ? ' active' : '')} onClick={() => setTab('vapaat')}>Vapaat <span className="seg-count">120</span></button>
        <button className={'seg-btn' + (tab === 'flagged' ? ' active' : '')} onClick={() => setTab('flagged')}>Priorisoidut <span className="seg-count">14</span></button>
        <button className={'seg-btn' + (tab === 'kuvaus' ? ' active' : '')} onClick={() => setTab('kuvaus')}>Kuvausta vailla <span className="seg-count">31</span></button>
      </div>
      {/* Toolbar — XAFin painikkeet mobiilille jaettuna */}
      <div className="tk-toolbar">
        <button className="tk-tool" onClick={() => onToolbar?.('lisaa-remontti')}>
          <IWrench size={16}/><span>Remontti</span>
        </button>
        <button className="tk-tool" onClick={() => onToolbar?.('lisaa-yleisesittely')}>
          <ICalendar size={16}/><span>Yleisesittely</span>
        </button>
        <button className="tk-tool" onClick={() => onToolbar?.('pikavaraus')}>
          <IBolt size={16}/><span>Pikavaraus</span>
        </button>
        <button className="tk-tool" onClick={() => onToolbar?.('laske')}>
          <ICalc size={16}/><span>Laske</span>
        </button>
      </div>
      <div className="ac-body">
        {apts.map(a => <TiskiCard key={a.id} apt={a} onMenu={onCardMenu} onOpen={onOpenApt}/>)}
      </div>
      <Tabbar active="tiskilista" onChange={onTab}/>
    </div>
  );
};

// ===================== TISKI-DETAIL =====================
const TiskiDetailScreen = ({ onBack }) => (
  <div className="ac-app">
    <Topbar
      title="Annankatu 5 A 2"
      leading={<button className="icon-btn" onClick={onBack}><IBack size={22}/></button>}
      trailing={<button className="icon-btn"><IKebab size={20}/></button>}
    />
    <div className="ac-body">
      <div className="hero-block">
        <div className="hero-pills">
          <span className="tk-pill tila">Vapaa</span>
          <span className="tk-pill needs"><ICamera size={11}/> Kuvaus</span>
          <span className="tk-pill needs"><ICheck size={11}/> Tarkastus</span>
        </div>
        <h1 className="hero-title">Annankatu 5 A 2</h1>
        <div className="hero-sub">PÄIVÄK. · 88,0 m² · 1.krs · vapaa 01.02.2028</div>
        <div className="hero-money">1 625,74 € / kk</div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Pikatoiminnot</div>
        <div className="quick-grid">
          <button className="quick-btn"><IBolt size={18}/><span>Pikavaraus</span></button>
          <button className="quick-btn"><ICalendar size={18}/><span>Yleisesittely</span></button>
          <button className="quick-btn"><IWrench size={18}/><span>Remontti</span></button>
          <button className="quick-btn"><ICamera size={18}/><span>Kuvaustilaus</span></button>
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Asunnon tiedot</div>
        <div className="kv-list">
          <div className="kv"><span>Tyyppi</span><b>PÄIVÄK.</b></div>
          <div className="kv"><span>Pinta-ala</span><b>88,0 m²</b></div>
          <div className="kv"><span>Kerros</span><b>1 / 4</b></div>
          <div className="kv"><span>Vuokra</span><b>1 625,74 €</b></div>
          <div className="kv"><span>Vapautumispäivä</span><b>01.02.2028</b></div>
          <div className="kv"><span>Poismuuttopäivä</span><b>31.01.2028</b></div>
          <div className="kv"><span>Hissi</span><b>—</b></div>
          <div className="kv"><span>Parveke</span><b>—</b></div>
          <div className="kv"><span>Sauna</span><b>—</b></div>
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Markkinointi</div>
        <div className="toggle-row">
          <span>Lumo.fi</span>
          <span className="toggle off">Pois</span>
        </div>
        <div className="toggle-row">
          <span>Vuokraovi</span>
          <span className="toggle off">Pois</span>
        </div>
        <div className="toggle-row">
          <span>On kuvaustarve</span>
          <span className="toggle on">Kyllä</span>
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Muistio</div>
        <div className="note-block">
          Asunnossa tehty pintaremontti 6/2025. Vuokranantajan toive: kuvaus uusiksi ennen lokakuuta.
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Lisätoiminnot</div>
        <button className="row-action"><span>Lisää tapahtuma</span><IChev size={16}/></button>
        <button className="row-action"><span>Avaa Talokeskus-linkit</span><IChev size={16}/></button>
        <button className="row-action"><span>Tarkastuksen tila</span><IChev size={16}/></button>
        <button className="row-action"><span>Laske tiskilistalla</span><IChev size={16}/></button>
      </div>
    </div>
    <div className="ac-footer-cta dual">
      <button className="btn-outline">Yleisesittely</button>
      <button className="btn-cta">Pikavaraus</button>
    </div>
  </div>
);

// ===================== CONTEXT MENU SHEET (alalaidasta nouseva) =====================
const ContextSheet = ({ title, subtitle, items, onClose }) => (
  <div className="sheet-backdrop" onClick={onClose}>
    <div className="sheet" onClick={(e) => e.stopPropagation()}>
      <div className="sheet-grip"/>
      <div className="sheet-head">
        <div className="sheet-title">{title}</div>
        {subtitle ? <div className="sheet-sub">{subtitle}</div> : null}
      </div>
      <div className="sheet-list">
        {items.map((it, i) => (
          <button key={i} className={'sheet-item' + (it.danger ? ' danger' : '')}>
            {it.icon}
            <span className="si-label">{it.label}</span>
            {it.shortcut ? <span className="si-short">{it.shortcut}</span> : null}
          </button>
        ))}
      </div>
      <button className="sheet-cancel" onClick={onClose}>Peru</button>
    </div>
  </div>
);

const TiskiContextSheet = ({ onClose }) => (
  <ContextSheet
    title="Annankatu 5 A 2"
    subtitle="Vapaa · 88 m² · PÄIVÄK."
    onClose={onClose}
    items={[
      { icon: <IBolt size={18}/>, label: 'Pikavaraus' },
      { icon: <ICalendar size={18}/>, label: 'Lisää yleisesittely' },
      { icon: <IWrench size={18}/>, label: 'Lisää remontti' },
      { icon: <ICamera size={18}/>, label: 'Tilaa valokuvaus' },
      { icon: <IPlus size={18}/>, label: 'Lisää tapahtuma' },
      { icon: <ICheck size={18}/>, label: 'Tarkastuksen tila' },
      { icon: <ILink size={18}/>, label: 'Talokeskus-linkit' },
      { icon: <ICalc size={18}/>, label: 'Laske tiskilistalla' },
    ]}
  />
);

const TehtavaContextSheet = ({ onClose }) => (
  <ContextSheet
    title="Tilaa keittiön remontti"
    subtitle="Käynnissä · High · 40%"
    onClose={onClose}
    items={[
      { icon: <ICheck size={18}/>, label: 'Merkitse valmiiksi' },
      { icon: <IClock size={18}/>, label: 'Postpone (siirrä myöhemmäksi)' },
      { icon: <IPlus size={18}/>, label: 'Päivitä valmiusaste' },
      { icon: <IUsers size={18}/>, label: 'Liitä käyttäjä' },
      { icon: <IBuilding size={18}/>, label: 'Avaa kohde' },
      { icon: <IX size={18}/>, label: 'Poista tehtävä', danger: true },
    ]}
  />
);

// ===================== ASUKKAAT =====================
const PeopleScreen = ({ onTab }) => {
  const people = [
    { name: 'Aalto, Eero', meta: 'Asukas · M-katu 8 B 12', initials: 'AE', tag: 'Sopimus voimassa' },
    { name: 'Ahonen, Eero', meta: 'Hakija · tarjous odottaa', initials: 'AH', tag: 'Allekirjoitus', tagColor: 'cta' },
    { name: 'Heikkilä, Sanna', meta: 'Asukas · Hämeentie 27 B 9', initials: 'HS' },
    { name: 'Korhonen, Liisa', meta: 'Hakija · varausesittely huomenna', initials: 'KL', tag: 'Esittely', tagColor: 'info' },
    { name: 'Lahtinen, Marko', meta: 'Asukas · Topeliuksenkatu 15', initials: 'LM' },
    { name: 'Niemi, Pekka', meta: 'Entinen asukas · irtisanominen', initials: 'NP', tag: 'Lähtee 31.7.' },
    { name: 'Salminen, Aino', meta: 'Asukas · Mechelininkatu 38', initials: 'SA' },
    { name: 'Virtanen, Maija', meta: 'Hakija · tutustumiskäynti tänään', initials: 'VM', tag: 'Tänään 09:00', tagColor: 'cta' },
  ];
  return (
    <div className="ac-app">
      <Topbar title="Asukkaat" trailing={<button className="icon-btn"><IFilter size={20}/></button>}/>
      <div className="ac-search">
        <ISearch size={18}/>
        <input placeholder="Hae nimellä, osoitteella…"/>
      </div>
      <div className="ac-body no-pad">
        {people.map((p, i) => (
          <div key={i} className="person-row">
            <span className="avatar">{p.initials}</span>
            <div className="info">
              <div className="name">{p.name}</div>
              <div className="meta">{p.meta}</div>
            </div>
            {p.tag ? <span className={'tag ' + (p.tagColor || '')}>{p.tag}</span> : null}
            <IChev size={16} className="chev"/>
          </div>
        ))}
      </div>
      <Tabbar active="people" onChange={onTab}/>
    </div>
  );
};

// ===================== MORE =====================
const MoreScreen = ({ onTab }) => {
  const Group = ({ title, children }) => (
    <>
      <div className="ds-eyebrow">{title}</div>
      <div className="more-list">{children}</div>
    </>
  );
  const Row = ({ icon, label, value }) => (
    <div className="more-row">
      {icon}
      <span className="label">{label}</span>
      {value ? <span className="value">{value}</span> : null}
      <IChev size={16} className="chev"/>
    </div>
  );
  return (
    <div className="ac-app">
      <Topbar title="Lisää"/>
      <div className="ac-body">
        <div className="me-card">
          <span className="avatar lg">AK</span>
          <div className="info">
            <div className="name">Anna Korhonen</div>
            <div className="meta">Asiakaspäällikkö · Helsinki Etelä</div>
          </div>
        </div>

        <Group title="ASMA">
          <Row icon={<ICalendar size={18}/>} label="Tarjoukset" value="12"/>
          <Row icon={<ICalendar size={18}/>} label="Tutustumiskäynnit"/>
          <Row icon={<ICalendar size={18}/>} label="Yleisesittelyt"/>
          <Row icon={<ICalendar size={18}/>} label="Varausesittelyt"/>
          <Row icon={<ICamera size={18}/>} label="Valokuvaukset"/>
          <Row icon={<IUsers size={18}/>} label="Liidit"/>
          <Row icon={<IUsers size={18}/>} label="Allekirjoitettavat" value="3"/>
          <Row icon={<ICheckbox size={18}/>} label="Saapuneet irtisanomiset"/>
        </Group>

        <Group title="KIRE">
          <Row icon={<IBuilding size={18}/>} label="Asuinhuoneisto"/>
          <Row icon={<IBuilding size={18}/>} label="Talousyksikkö"/>
          <Row icon={<IWrench size={18}/>} label="Remontit"/>
        </Group>

        <Group title="Sovellus">
          <Row label="Teema" value="Vaalea"/>
          <Row label="Kieli" value="Suomi"/>
          <Row label="Ilmoitukset" value="3 tyyppiä"/>
        </Group>

        <button className="btn-outline danger" style={{marginTop: 12}}>Kirjaudu ulos</button>
        <div className="footer-version">Lumo mVasu · v0.1.0</div>
      </div>
      <Tabbar active="more" onChange={onTab}/>
    </div>
  );
};

// ===================== HOME HUB (accordion + tile grid) =====================
// Vaihtoehtoinen aloitusnäkymä: dx-accordion, jokaisessa paneelissa tile-ruudukko.
// Ylätason linkit (Käyttäjän tehtävälista, Koti, Tiskilista, Tarjoukset…) näkyvät yhtenä
// "Päätoiminnot"-paneelina aukinaisena. ASMA ja KIRE ovat omia paneelejaan, oletuksena kiinni.

const HubTile = ({ icon, label, count, accent, onClick }) => (
  <button className={'hub-tile' + (accent ? ' accent-' + accent : '')} onClick={onClick}>
    <span className="ht-icon">{icon}</span>
    <span className="ht-label">{label}</span>
    {count != null ? <span className="ht-count">{count}</span> : null}
  </button>
);

const AccordionPanel = ({ title, subtitle, open, onToggle, count, children }) => (
  <div className={'accordion-panel' + (open ? ' open' : '')}>
    <button className="ap-head" onClick={onToggle}>
      <div className="ap-titles">
        <span className="ap-title">{title}</span>
        {subtitle ? <span className="ap-sub">{subtitle}</span> : null}
      </div>
      {count != null ? <span className="ap-count">{count}</span> : null}
      <IChev size={16} className="ap-chev"/>
    </button>
    {open ? <div className="ap-body">{children}</div> : null}
  </div>
);

const HomeHubScreen = ({ onTab }) => {
  const [open, setOpen] = useState({ paa: true, asma: false, kire: false });
  const tg = (k) => setOpen(s => ({ ...s, [k]: !s[k] }));
  return (
    <div className="ac-app">
      <Topbar
        eyebrow="Hyvää huomenta, Anna"
        title="Koti"
        trailing={
          <>
            <button className="icon-btn"><ISearch size={20}/></button>
            <button className="icon-btn"><IBell size={20}/></button>
          </>
        }
      />
      <div className="ac-body">
        <AccordionPanel
          title="Päätoiminnot"
          subtitle="Päivittäinen työ"
          open={open.paa}
          onToggle={() => tg('paa')}
        >
          <div className="hub-grid">
            <HubTile icon={<ICheckbox size={22}/>} label="Käyttäjän tehtävälista" count={5} accent="cta"/>
            <HubTile icon={<IList size={22}/>} label="Tiskilista" count={120}/>
            <HubTile icon={<IFile size={22}/>} label="Tarjoukset" count={12} accent="warn"/>
            <HubTile icon={<ICalendar size={22}/>} label="Varausesittelyt" count={3}/>
            <HubTile icon={<ICalendar size={22}/>} label="Tutustumiskäynnit" count={4}/>
            <HubTile icon={<ICalendar size={22}/>} label="Yleisesittelyt" count={2}/>
            <HubTile icon={<IKey size={22}/>} label="Autopaikka"/>
          </div>
        </AccordionPanel>

        <AccordionPanel
          title="ASMA"
          subtitle="Asiakkuus & sopimukset"
          count={26}
          open={open.asma}
          onToggle={() => tg('asma')}
        >
          <div className="hub-grid">
            <HubTile icon={<ICamera size={22}/>} label="Valokuvaukset" count={8}/>
            <HubTile icon={<IUsers size={22}/>} label="Liidit" count={14}/>
            <HubTile icon={<IUsers size={22}/>} label="Henkilöt"/>
            <HubTile icon={<ISignature size={22}/>} label="Allekirjoitettavat" count={3} accent="cta"/>
            <HubTile icon={<IFile size={22}/>} label="Sopimukset"/>
            <HubTile icon={<ISignature size={22}/>} label="Sähköiset allekirjoitukset"/>
            <HubTile icon={<IBuilding size={22}/>} label="Lumo Verkkokauppa"/>
            <HubTile icon={<IMail size={22}/>} label="Saapuneet irtisanomiset" count={1} accent="cta"/>
            <HubTile icon={<IUsers size={22}/>} label="Yhteyshenkilö"/>
            <HubTile icon={<IBuilding size={22}/>} label="Yritys"/>
          </div>
        </AccordionPanel>

        <AccordionPanel
          title="KIRE"
          subtitle="Kiinteistöt & remontit"
          open={open.kire}
          onToggle={() => tg('kire')}
        >
          <div className="hub-grid">
            <HubTile icon={<IBuilding size={22}/>} label="Asuinhuoneisto"/>
            <HubTile icon={<IBuilding size={22}/>} label="Talousyksikkö"/>
            <HubTile icon={<IWrench size={22}/>} label="Remontit" count={7} accent="warn"/>
          </div>
        </AccordionPanel>
      </div>
      <Tabbar active="tasks" onChange={onTab}/>
    </div>
  );
};

// pieniä apukuvakkeita uudelle näytölle
const ISignature = (p) => <I {...p}><path d="M3 17c2-1 4-3 6-3s4 2 5 0 1-5 3-5 4 4 4 4"/><path d="M3 21h18"/></I>;
const IMail = (p) => <I {...p}><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/></I>;
const IFile = (p) => <I {...p}><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/></I>;

Object.assign(window, {
  TasksScreen, TehtavaDetailScreen, TehtavaContextSheet,
  TiskilistaScreen, TiskiDetailScreen, TiskiContextSheet,
  HomeHubScreen,
  PeopleScreen, MoreScreen,
});
