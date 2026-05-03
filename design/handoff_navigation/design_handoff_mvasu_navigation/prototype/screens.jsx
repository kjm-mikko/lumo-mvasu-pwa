/* global React */
const { useState } = React;

const I = ({ d, size = 22, vb = '0 0 24 24', children, ...rest }) => (
  <svg width={size} height={size} viewBox={vb} fill="none" stroke="currentColor"
       strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" {...rest}>
    {d ? <path d={d}/> : children}
  </svg>
);

// Bottom-tab icons
const IInbox = (p) => <I {...p}><path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11Z"/></I>;
const IUsers = (p) => <I {...p}><circle cx="9" cy="8" r="3.5"/><circle cx="17.5" cy="9.5" r="2.5"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><path d="M15 17c.5-1.8 2.4-3 4.5-3 1.2 0 2.5.5 2.5 3"/></I>;
const IBuilding = (p) => <I {...p}><path d="M4 21V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v15"/><path d="M16 9h2a2 2 0 0 1 2 2v10"/><path d="M8 8h.01M8 12h.01M8 16h.01M12 8h.01M12 12h.01M12 16h.01M16 13h.01M16 17h.01"/></I>;
const IFile = (p) => <I {...p}><path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5"/></I>;
const IMore = (p) => <I {...p}><circle cx="12" cy="12" r="1.5" fill="currentColor"/><circle cx="5" cy="12" r="1.5" fill="currentColor"/><circle cx="19" cy="12" r="1.5" fill="currentColor"/></I>;
// Task card icons
const ICalendar = (p) => <I {...p}><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 9h18M8 3v4M16 3v4"/></I>;
const ISignature = (p) => <I {...p}><path d="M3 17c2-1 4-3 6-3s4 2 5 0 1-5 3-5 4 4 4 4"/><path d="M3 21h18"/></I>;
const IPhone = (p) => <I {...p}><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.86 19.86 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.86 19.86 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.37 1.9.72 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.91.35 1.85.59 2.81.72A2 2 0 0 1 22 16.92Z"/></I>;
const IMail = (p) => <I {...p}><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/></I>;
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

const Tabbar = ({ active, onChange }) => {
  const tabs = [
    { id: 'tasks', label: 'Tehtävät', icon: IInbox },
    { id: 'people', label: 'Asukkaat', icon: IUsers },
    { id: 'units', label: 'Kohteet', icon: IBuilding },
    { id: 'docs', label: 'Sopimukset', icon: IFile },
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

// ===================== TASKS (queue) =====================
const TaskCard = ({ when, type, title, who, meta, actions, urgent, accent = 'navy' }) => (
  <div className={'task-card accent-' + accent + (urgent ? ' urgent' : '')}>
    <div className="when">
      <span className="time">{when.time}</span>
      {when.note ? <span className="note">{when.note}</span> : null}
    </div>
    <div className="body">
      <div className="type-row">
        <span className="type-icon">{type.icon}</span>
        <span className="type-label">{type.label}</span>
        {urgent ? <span className="urgent-pill"><IFlame size={12}/> Kiireellinen</span> : null}
      </div>
      <div className="title">{title}</div>
      {who ? <div className="who">{who}</div> : null}
      {meta ? <div className="meta">{meta}</div> : null}
      {actions ? (
        <div className="actions">
          {actions.map((a, i) => (
            <button key={i} className={'a-btn' + (a.primary ? ' primary' : '')}>{a.label}</button>
          ))}
        </div>
      ) : null}
    </div>
  </div>
);

const TasksScreen = () => {
  const [tab, setTab] = useState('tasks');
  return (
    <div className="ac-app">
      <Topbar
        eyebrow="Hyvää huomenta, Anna"
        title="Tehtävät"
        trailing={
          <>
            <button className="icon-btn"><ISearch size={20}/></button>
            <button className="icon-btn"><IBell size={20}/></button>
          </>
        }
      />
      <div className="ac-body">
        <div className="day-header">
          <span className="day">Tänään</span>
          <span className="date">to 4.6.</span>
          <span className="count">5 tehtävää</span>
        </div>

        <TaskCard
          when={{ time: '09:00' }}
          type={{ icon: <ICalendar size={14}/>, label: 'Tutustumiskäynti' }}
          title="Mannerheimintie 12 A 4"
          who="Maija Virtanen · 044 123 4567"
          meta="3 h kuluttua · 15 min ajomatka"
          actions={[
            { label: 'Avaa kohde', primary: true },
            { label: 'Soita' },
          ]}
        />

        <TaskCard
          when={{ time: '11:30' }}
          type={{ icon: <ICalendar size={14}/>, label: 'Yleisesittely' }}
          title="Mechelininkatu 38"
          meta="3 ilmoittautunutta · 2 vapaata"
          actions={[
            { label: 'Avaa esittely', primary: true },
          ]}
        />

        <TaskCard
          when={{ time: 'Heti', note: 'odottaa 2 pv' }}
          type={{ icon: <ISignature size={14}/>, label: 'Allekirjoitusta odottaa' }}
          title="4 tarjousta valmiina"
          meta="Vanhin: Eero Ahonen · M-katu 8 B 12"
          urgent
          accent="cta"
          actions={[
            { label: 'Käsittele', primary: true },
          ]}
        />

        <TaskCard
          when={{ time: 'Heti' }}
          type={{ icon: <IMail size={14}/>, label: 'Saapunut irtisanominen' }}
          title="Lähikatu 8 B 12"
          meta="Vastaanotettu 30 min sitten · ei vielä avattu"
          accent="info"
          actions={[
            { label: 'Lue', primary: true },
          ]}
        />

        <TaskCard
          when={{ time: '15:00' }}
          type={{ icon: <ICamera size={14}/>, label: 'Valokuvaus tilattu' }}
          title="Topeliuksenkatu 15 C 22"
          meta="Kuvaaja: T. Koskinen · vahvistettu"
          actions={[
            { label: 'Avaa', primary: true },
          ]}
        />

        <div className="day-header">
          <span className="day">Huomenna</span>
          <span className="date">pe 5.6.</span>
          <span className="count">3 tehtävää</span>
        </div>

        <TaskCard
          when={{ time: '10:00' }}
          type={{ icon: <ICalendar size={14}/>, label: 'Varausesittely' }}
          title="Hämeentie 27 B 9"
          who="Liisa Korhonen"
          meta="Varattu 2 vrk sitten"
        />

        <TaskCard
          when={{ time: '13:00' }}
          type={{ icon: <IWrench size={14}/>, label: 'Remontti vahvistettava' }}
          title="Pengerkatu 3 A 1"
          meta="Kustannusarvio 4 200 € · vaatii hyväksynnän"
          accent="warn"
          actions={[
            { label: 'Tarkista', primary: true },
          ]}
        />
      </div>
      <Tabbar active={tab} onChange={setTab}/>
    </div>
  );
};

// ===================== ASUKKAAT (browse list) =====================
const PeopleScreen = () => {
  const [tab, setTab] = useState('people');
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
      <Tabbar active={tab} onChange={setTab}/>
    </div>
  );
};

// ===================== TASK DETAIL =====================
const TaskDetailScreen = () => (
  <div className="ac-app">
    <Topbar
      title="Tutustumiskäynti"
      leading={<button className="icon-btn"><IBack size={22}/></button>}
    />
    <div className="ac-body">
      <div className="hero-block">
        <div className="hero-time">
          <IClock size={16}/>
          <span>Tänään klo 09:00</span>
          <span className="hero-time-sep">·</span>
          <span>3 h kuluttua</span>
        </div>
        <h1 className="hero-title">Mannerheimintie 12 A 4</h1>
        <div className="hero-sub">2 h, 47 m² · vapaa 1.7. alkaen</div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Asiakas</div>
        <div className="contact-card">
          <span className="avatar">VM</span>
          <div className="info">
            <div className="name">Maija Virtanen</div>
            <div className="meta">044 123 4567 · maija.v@example.fi</div>
          </div>
          <button className="icon-btn-circle"><IPhone size={18}/></button>
        </div>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Toiminnot</div>
        <button className="row-action"><span>Avaa kohde Lumo Verkossa</span><IChev size={16}/></button>
        <button className="row-action"><span>Tee tarjous tästä</span><IChev size={16}/></button>
        <button className="row-action"><span>Merkitse pidetyksi</span><IChev size={16}/></button>
        <button className="row-action danger"><span>Peruuta käynti</span><IChev size={16}/></button>
      </div>

      <div className="detail-section">
        <div className="ds-eyebrow">Muistiinpanot</div>
        <div className="note-block">
          Asiakas on muuttamassa Tampereelta 1.7. Toivoo nähdä myös 3h-vaihtoehdon samasta talosta jos vapautuu.
        </div>
      </div>
    </div>
    <div className="ac-footer-cta">
      <button className="btn-cta">Avaa kohde</button>
    </div>
  </div>
);

// ===================== QUICK SEARCH (Cmd+K equivalent) =====================
const QuickSearchScreen = () => (
  <div className="ac-app">
    <div className="qs-bar">
      <ISearch size={18}/>
      <input autoFocus defaultValue="manner" placeholder="Etsi mistä tahansa…"/>
      <button className="qs-cancel">Peru</button>
    </div>
    <div className="ac-body no-pad">
      <div className="qs-section">Kohteet</div>
      <div className="qs-row">
        <IBuilding size={18}/>
        <div className="info">
          <div className="title"><mark>Manner</mark>heimintie 12 A 4</div>
          <div className="meta">2 h · 47 m² · vapaa 1.7.</div>
        </div>
      </div>
      <div className="qs-row">
        <IBuilding size={18}/>
        <div className="info">
          <div className="title"><mark>Manner</mark>heimintie 12 B 7</div>
          <div className="meta">3 h · 68 m² · varattu</div>
        </div>
      </div>

      <div className="qs-section">Asukkaat</div>
      <div className="qs-row">
        <span className="avatar small">VM</span>
        <div className="info">
          <div className="title">Virtanen, Maija</div>
          <div className="meta">Hakija · <mark>Manner</mark>heimintie 12 A 4 · tänään 09:00</div>
        </div>
      </div>

      <div className="qs-section">Toiminnot</div>
      <div className="qs-row action">
        <IPlus size={18}/>
        <div className="info">
          <div className="title">Tee uusi tarjous</div>
          <div className="meta">Aloita tyhjältä lomakkeelta</div>
        </div>
      </div>
      <div className="qs-row action">
        <ICalendar size={18}/>
        <div className="info">
          <div className="title">Varaa esittely</div>
          <div className="meta">Sovi aika hakijan kanssa</div>
        </div>
      </div>
    </div>
  </div>
);

// ===================== MORE (settings + modules) =====================
const MoreScreen = () => {
  const [tab, setTab] = useState('more');
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

        <Group title="Aineisto">
          <Row icon={<IFile size={18}/>} label="Tarjoukset" value="12 avointa"/>
          <Row icon={<IBuilding size={18}/>} label="Asunnot vapaana" value="34"/>
          <Row icon={<ICalendar size={18}/>} label="Esittelykalenteri"/>
          <Row icon={<IWrench size={18}/>} label="Remontit"/>
          <Row icon={<ICamera size={18}/>} label="Valokuvaukset"/>
        </Group>

        <Group title="Sovellus">
          <Row label="Teema" value="Vaalea"/>
          <Row label="Kieli" value="Suomi"/>
          <Row label="Sijaintipalvelut" value="Sallittu"/>
          <Row label="Ilmoitukset" value="3 tyyppiä"/>
        </Group>

        <button className="btn-outline danger" style={{marginTop: 12}}>Kirjaudu ulos</button>
        <div className="footer-version">Lumo mVasu · v0.1.0</div>
      </div>
      <Tabbar active={tab} onChange={setTab}/>
    </div>
  );
};

Object.assign(window, {
  TasksScreen, PeopleScreen, TaskDetailScreen, QuickSearchScreen, MoreScreen,
});
