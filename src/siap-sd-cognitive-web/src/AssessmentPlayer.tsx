import { useEffect, useRef, useState, type ReactNode } from 'react'
import { apiBaseUrl } from './api'

type Child = { id: string; name: string }
type Option = { code: string; text: string }
type Question = { id: string; questionType: string; instruction: string; questionText: string; stimulusJson?: string; options: Option[] }

const memoryTypes = new Set(['MemorySequence', 'SpatialMemory', 'AuditorySequence', 'InstructionSequence'])
const attentionTypes = new Set(['TargetDetection', 'VisualSearch'])
const planningTypes = new Set(['GridPlanning'])
const executiveTypes = new Set(['InhibitoryControl', 'RuleSwitch'])
const memoryDelay = (configured: number | undefined, fallback: number) => (window as Window & { __E2E_MEMORY_DELAY__?: number }).__E2E_MEMORY_DELAY__ ?? configured ?? fallback

export default function AssessmentPlayer() {
  const [children, setChildren] = useState<Child[]>([])
  const [selectedChildId, setSelectedChildId] = useState('')
  const [newChildName, setNewChildName] = useState('')
  const [birthDate, setBirthDate] = useState('2020-01-01')
  const [sessionId, setSessionId] = useState('')
  const [question, setQuestion] = useState<Question | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [reportMode, setReportMode] = useState<'parent' | 'detail' | null>(null)
  const questionBecameInteractiveAt = useRef(0)
  const shell = (content: ReactNode) => <main className="assessment-shell"><header className="brand-bar"><div className="brand"><span className="brand-mark" aria-hidden="true">✦</span><span>SiapSD Cognitive</span></div><div className="progress-copy">Aktivitas bermain sambil belajar<div className="progress-dots" aria-hidden="true"><span/><span/><span/><span/></div></div></header>{content}</main>

  useEffect(() => {
    fetch(`${apiBaseUrl}/api/children`).then(response => response.json()).then(setChildren)
  }, [])

  async function startAssessment() {
    const blueprints = await fetch(`${apiBaseUrl}/api/assessment-blueprints`).then(response => response.json())
    const session = await fetch(`${apiBaseUrl}/api/assessments/start`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ childId: selectedChildId, blueprintId: blueprints[0].id })
    }).then(response => response.json())

    setSessionId(session.id)
    await loadNextQuestion(session.id)
  }

  async function createChild() {
    const child = await fetch(`${apiBaseUrl}/api/children`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: newChildName, birthDate })
    }).then(response => response.json()) as Child

    setChildren(currentChildren => [...currentChildren, child])
    setSelectedChildId(child.id)
    setNewChildName('')
  }

  async function loadNextQuestion(activeSessionId = sessionId) {
    const response = await fetch(`${apiBaseUrl}/api/assessments/${activeSessionId}/next-question`)
    if (response.status === 204) {
      setQuestion(null)
      return
    }

    const nextQuestion = await response.json() as Question
    setQuestion(nextQuestion)
    questionBecameInteractiveAt.current = performance.now()
  }

  async function submitAnswer(answer: string, responseStartedAt = questionBecameInteractiveAt.current) {
    if (!question) return
    setIsSubmitting(true)
    await fetch(`${apiBaseUrl}/api/assessments/${sessionId}/answer`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ questionId: question.id, answer, responseTimeMs: Math.max(1, Math.round(performance.now() - responseStartedAt)), firstInteraction: true })
    })
    setIsSubmitting(false)
    await loadNextQuestion()
  }

  if (!sessionId) return shell(<section className="start-screen"><p className="eyebrow">Aktivitas interaktif untuk usia 5–7 tahun</p><h1>Yuk, kenali cara belajar anak.</h1><p>Lewat permainan singkat, SiapSD Cognitive membantu melihat pola kemampuan anak dengan cara yang menyenangkan.</p><div className="form-grid"><label htmlFor="child-name">Nama anak</label><input id="child-name" aria-label="Nama anak" value={newChildName} onChange={event => setNewChildName(event.target.value)} placeholder="Tulis nama anak"/><label htmlFor="birth-date">Tanggal lahir</label><input id="birth-date" aria-label="Tanggal lahir" type="date" value={birthDate} max={new Date().toISOString().slice(0, 10)} onChange={event => setBirthDate(event.target.value)}/><button disabled={!newChildName.trim() || !birthDate} onClick={createChild}>Tambah anak</button><label htmlFor="child-select">Pilih anak</label><select id="child-select" value={selectedChildId} onChange={event => setSelectedChildId(event.target.value)}><option value="">Pilih anak untuk memulai</option>{children.map(child => <option key={child.id} value={child.id}>{child.name}</option>)}</select></div><div className="action-row"><button disabled={!selectedChildId} onClick={startAssessment}>Mulai assessment</button></div></section>)
  if (!question) return shell(reportMode ? <ReportView sessionId={sessionId} detailed={reportMode === 'detail'} onBack={() => setReportMode(null)} /> : <section className="assessment-complete" data-testid="assessment-complete"><h2>Selesai!</h2><p>Kamu sudah menyelesaikan semua aktivitas. Terima kasih sudah bermain bersama kami.</p><div className="action-row"><button onClick={() => setReportMode('parent')}>Laporan untuk Orang Tua</button><button onClick={() => setReportMode('detail')}>Laporan Rinci</button></div></section>)
  if (memoryTypes.has(question.questionType)) return shell(<div data-testid="memory-player"><MemoryQuestion question={question} onSubmit={submitAnswer} /></div>)
  if (attentionTypes.has(question.questionType)) return shell(<AttentionQuestion question={question} submitting={isSubmitting} onSubmit={submitAnswer} />)
  if (planningTypes.has(question.questionType)) return shell(<PlanningQuestion question={question} submitting={isSubmitting} onSubmit={submitAnswer} />)
  if (executiveTypes.has(question.questionType)) return shell(<ExecutiveQuestion question={question} submitting={isSubmitting} onSubmit={submitAnswer} />)
  if (question.questionType === 'SymbolMatching') return shell(<SpeedQuestion question={question} submitting={isSubmitting} onSubmit={submitAnswer} />)
  return shell(<StandardQuestion question={question} disabled={isSubmitting} onSubmit={submitAnswer} />)
}

function ExecutiveQuestion({ question, submitting, onSubmit }: { question: Question; submitting: boolean; onSubmit: (answer: string) => Promise<void> }) { const [selected,setSelected]=useState(''); const stimulus=JSON.parse(question.stimulusJson ?? '{}') as { rule?: string; stimulus?: { display?: string } }; return <section className="player" data-testid="executive-player" data-question-id={question.id}><p data-testid="executive-rule">{question.instruction}</p><div aria-label="Kartu" style={{fontSize:'3rem'}}>{stimulus.stimulus?.display}</div><h2>{question.questionText}</h2><div className="options">{question.options.map(o=><button key={o.code} aria-pressed={selected===o.code} disabled={submitting} onClick={()=>setSelected(o.code)}>{o.text}</button>)}</div><button disabled={!selected||submitting} onClick={()=>onSubmit(selected)}>Kirim</button></section> }

function SpeedQuestion({ question, submitting, onSubmit }: { question: Question; submitting: boolean; onSubmit: (answer: string) => Promise<void> }) { const [selected,setSelected]=useState(''); const [seconds,setSeconds]=useState(60); useEffect(()=>{const duration=(window as Window & {__E2E_SPEED_DURATION__?:number}).__E2E_SPEED_DURATION__??60;setSeconds(duration);const id=window.setInterval(()=>setSeconds(v=>Math.max(0,v-1)),1000);return()=>window.clearInterval(id)},[question.id]); return <section className="player" data-testid="processing-speed" data-question-id={question.id}><p>Sisa waktu: {seconds} dtk</p><h2>{question.questionText}</h2><div className="options">{question.options.map(o=><button key={o.code} aria-pressed={selected===o.code} disabled={submitting} onClick={()=>setSelected(o.code)}>{o.text}</button>)}</div><button disabled={!selected||submitting} onClick={()=>onSubmit(selected)}>Lanjut</button></section> }

function ReportView({ sessionId, detailed, onBack }: { sessionId: string; detailed: boolean; onBack: () => void }) {
  type Metric = { questionType: string; metrics: Record<string, string | number | boolean | null> }
  type Domain = { name: string; subtests: { name: string; metrics: Metric[] }[] }
  const [report, setReport] = useState<{ child?: { name?: string }; domains?: Domain[] } | null>(null)
  const [failed, setFailed] = useState(false)
  useEffect(() => { fetch(`${apiBaseUrl}/api/reports/${sessionId}`).then(r => { if (!r.ok) throw new Error('Report unavailable'); return r.json() }).then(setReport).catch(() => setFailed(true)) }, [sessionId])
  if (failed) return <section className="player report-view" data-testid={detailed ? 'detailed-report' : 'parent-report'}><h2>Ups, ada kendala.</h2><p>Kami belum bisa menyiapkan laporan. Coba lagi sebentar.</p><button onClick={onBack}>Kembali</button></section>
  if (!report) return <section className="player report-view" data-testid={detailed ? 'detailed-report' : 'parent-report'}><p className="loading-copy">Menyiapkan laporan...</p></section>
  const labels: Record<string, string> = { trialsAttempted: 'Jumlah percobaan', fullyCorrectTrials: 'Percobaan benar penuh', partialCorrectTrials: 'Percobaan sebagian benar', highestSpanAttempted: 'Rentang tertinggi', highestSpanFullyCorrect: 'Rentang benar penuh', highestStableSpan: 'Rentang stabil', averagePositionAccuracy: 'Akurasi posisi', medianResponseTimeMs: 'Median waktu respons (ms)', targetCount: 'Target', correctHits: 'Ditemukan', hits: 'Ditemukan', missedTargets: 'Terlewat', misses: 'Terlewat', falsePositives: 'Salah pilih', accuracy: 'Akurasi', completionTimeMs: 'Waktu (ms)', correctResponses: 'Respons benar', incorrectResponses: 'Respons salah', inhibitionErrors: 'Kesalahan penghambatan', ruleSwitchCount: 'Perubahan aturan', switchErrors: 'Kesalahan ganti aturan', perseverationErrors: 'Kesalahan berulang', switchLatencyMs: 'Waktu ganti aturan (ms)', optimalSteps: 'Langkah optimal', stepsProgrammed: 'Langkah diprogram', executedSteps: 'Langkah dijalankan', wrongSteps: 'Langkah salah', undoCount: 'Undo', resetCount: 'Reset', runCount: 'Jalankan', planningTimeMs: 'Waktu merencanakan (ms)', solved: 'Tujuan tercapai', attempted: 'Dicoba', correct: 'Benar', incorrect: 'Salah', correctPerMinute: 'Benar per menit', durationMs: 'Durasi (ms)' }
  const summary = (metric: Metric) => { const value = metric.metrics; if (metric.questionType === 'GridPlanning') return value.solved ? `Anak berhasil mencapai tujuan dengan ${value.stepsProgrammed} langkah. Jalur optimal adalah ${value.optimalSteps} langkah.` : `Anak mencoba menyusun ${value.stepsProgrammed} langkah menuju tujuan.`; if (metric.questionType === 'SymbolMatching') return `Anak menyelesaikan ${value.attempted} item dengan akurasi ${value.accuracy}%.`; if (metric.questionType === 'TargetDetection' || metric.questionType === 'VisualSearch') return `Anak menemukan ${value.correctHits ?? value.hits ?? 0} dari ${value.targetCount ?? 0} target dengan tepat.`; if (metric.questionType === 'InhibitoryControl' || metric.questionType === 'RuleSwitch') return `Anak mengikuti aturan tugas dengan akurasi ${value.accuracy}%.`; return `Anak dapat mengingat urutan hingga ${value.highestStableSpan ?? value.sequenceLength ?? 0} stimulus secara konsisten.` }
  return <section className="player report-view" data-testid={detailed ? 'detailed-report' : 'parent-report'}><h2>{detailed ? 'Laporan Rinci' : 'Laporan untuk Orang Tua'}</h2><p>{report.child?.name ? `Sesi ${report.child.name}.` : 'Ringkasan sesi.'}</p>{report.domains?.map(domain => <article key={domain.name}><h3>{domain.name}</h3>{domain.subtests.map(subtest => <section key={subtest.name}><h4>{subtest.name}</h4>{subtest.metrics.map((metric, index) => detailed ? <dl key={index}>{Object.entries(metric.metrics).filter(([key]) => key !== 'benar' && key !== 'waktuResponsMs').map(([key, value]) => <div key={key}><dt>{labels[key] ?? key}</dt><dd>{String(value ?? '-')}</dd></div>)}</dl> : <p key={index}>{summary(metric)}</p>)}</section>)}</article>)}<p className="disclaimer">Hasil ini merupakan profil performa pada sesi assessment dan bukan diagnosis atau skor IQ klinis.</p><div className="action-row"><button onClick={onBack}>Kembali</button></div></section>
}

function ReportViewLegacy({ sessionId, detailed, onBack }: { sessionId: string; detailed: boolean; onBack: () => void }) {
  const [report, setReport] = useState<{ child?: { name?: string }; duration?: number; domainScores?: { score: { rawScore: number; internalScore: number; metricsJson: string } }[] } | null>(null)
  useEffect(() => { fetch(`${apiBaseUrl}/api/reports/${sessionId}`).then(r => r.json()).then(setReport).catch(() => setReport(null)) }, [sessionId])
  if (!report) return <section className="player"><p>Menyiapkan laporan…</p></section>
  return <section className="player" data-testid={detailed ? 'detailed-report' : 'parent-report'}><h2>{detailed ? 'Laporan Rinci' : 'Laporan untuk Orang Tua'}</h2><p>{report.child?.name ? `Sesi ${report.child.name}.` : 'Ringkasan sesi.'} Ini adalah catatan perkembangan, bukan diagnosis.</p>{report.domainScores?.map((item, index) => <article key={index}><h3>Area {index + 1}</h3><p>{detailed ? `Jawaban tepat: ${item.score.rawScore}; ketepatan: ${item.score.internalScore}%.` : `Pada sesi ini, anak menyelesaikan kegiatan dengan ketepatan ${item.score.internalScore}%.`}</p></article>)}<button onClick={onBack}>Kembali</button></section>
}

void ReportViewLegacy

function PlanningQuestion({ question, submitting, onSubmit }: { question: Question; submitting: boolean; onSubmit: (answer: string) => Promise<void> }) {
  const task = JSON.parse(question.stimulusJson ?? '{}') as { rows?: number; columns?: number; start?: number[]; goal?: number[]; obstacles?: number[][] }
  const start = task.start ?? [0, 0], goal = task.goal ?? [2, 2], obstacles = task.obstacles ?? []
  const [commands, setCommands] = useState<string[]>([])
  const [position, setPosition] = useState(start)
  const [undoCount, setUndoCount] = useState(0)
  const [resetCount, setResetCount] = useState(0)
  const [runCount, setRunCount] = useState(0)
  const startedAt = useRef(performance.now())
  useEffect(() => { startedAt.current = performance.now(); setCommands([]); setPosition(start); setUndoCount(0); setResetCount(0); setRunCount(0) }, [question.id])
  const run = () => { setRunCount(value => value + 1); let next = [...start]; setPosition([...start]); commands.forEach((command, index) => window.setTimeout(() => { const delta: Record<string, number[]> = { Up: [-1, 0], Down: [1, 0], Left: [0, -1], Right: [0, 1] }; const d = delta[command]; const candidate = [next[0] + d[0], next[1] + d[1]]; if (candidate[0] >= 0 && candidate[1] >= 0 && candidate[0] < (task.rows ?? 3) && candidate[1] < (task.columns ?? 3) && !obstacles.some(o => o[0] === candidate[0] && o[1] === candidate[1])) { next = candidate; setPosition([...next]) } }, (index + 1) * 250)) }
  const submit = () => onSubmit(JSON.stringify({ commands, undoCount, resetCount, runCount, wrongSteps: 0, planningTimeMs: Math.max(1, Math.round(performance.now() - startedAt.current)) }))
  const rows=task.rows ?? 3, columns=task.columns ?? 3
  return <section className="player" data-testid="planning-player" data-question-id={question.id}><p>{question.instruction}</p><h2>{question.questionText}</h2><div data-testid="planning-grid" aria-label="Peta permainan" style={{display:'grid',gridTemplateColumns:`repeat(${columns}, 3rem)`,gap:4}}>{Array.from({length:rows*columns},(_,i)=>{const cell=[Math.floor(i/columns),i%columns];const here=position[0]===cell[0]&&position[1]===cell[1], isGoal=goal[0]===cell[0]&&goal[1]===cell[1], blocked=obstacles.some(o=>o[0]===cell[0]&&o[1]===cell[1]);return <div key={i} data-testid={here?'planning-player':isGoal?'planning-goal':blocked?'planning-obstacle':undefined} style={{height:'3rem',border:'1px solid #777',display:'grid',placeItems:'center',fontSize:'2rem'}}>{here?'🤖':isGoal?'⭐':blocked?'⬛':'⬜'}</div>})}</div><div className="options">{['Up', 'Down', 'Left', 'Right'].map(command => <button data-testid={`command-${command.toLowerCase()}`} key={command} disabled={submitting} onClick={() => setCommands(value => [...value, command])}>{({ Up: '↑ Atas', Down: '↓ Bawah', Left: '← Kiri', Right: '→ Kanan' } as Record<string, string>)[command]}</button>)}</div><p data-testid="planning-command-sequence">{commands.map(c=>({Up:'↑',Down:'↓',Left:'←',Right:'→'}[c])).join(' ') || 'Belum ada langkah'}</p><button data-testid="undo" disabled={!commands.length || submitting} onClick={() => { setCommands(value => value.slice(0, -1)); setUndoCount(value => value + 1) }}>Undo</button><button data-testid="reset" disabled={submitting} onClick={() => { setCommands([]); setPosition(start); setResetCount(value => value + 1) }}>Reset</button><button data-testid="run" disabled={!commands.length || submitting} onClick={run}>Jalankan</button><button data-testid="submit" disabled={submitting} onClick={submit}>Kirim</button></section>
}

function PlanningQuestionLegacy({ question, submitting, onSubmit }: { question: Question; submitting: boolean; onSubmit: (answer: string) => Promise<void> }) {
  const [commands, setCommands] = useState<string[]>([])
  const [position, setPosition] = useState([0, 0])
  const [undoCount, setUndoCount] = useState(0)
  const [resetCount, setResetCount] = useState(0)
  const [runCount, setRunCount] = useState(0)
  const planningStartedAt = useRef(performance.now())
  const task = JSON.parse(question.stimulusJson ?? '{}') as { rows?: number; columns?: number; start?: number[]; goal?: number[]; obstacles?: number[][] }
  const start = task.start ?? [0, 0], goal = task.goal ?? [2, 2], obstacles = task.obstacles ?? []
  useEffect(() => { planningStartedAt.current = performance.now(); setCommands([]); setPosition(start); setUndoCount(0); setResetCount(0); setRunCount(0) }, [question.id])
  const run = () => { setRunCount(value => value + 1); let next = [...start]; for (const c of commands) { const delta: Record<string, number[]> = { Up: [-1, 0], Down: [1, 0], Left: [0, -1], Right: [0, 1] }; const d = delta[c]; const candidate = [next[0] + d[0], next[1] + d[1]]; if (candidate[0] >= 0 && candidate[1] >= 0 && candidate[0] < (task.rows ?? 3) && candidate[1] < (task.columns ?? 3) && !obstacles.some(o => o[0] === candidate[0] && o[1] === candidate[1])) next = candidate }; setPosition(next) }
  const submit = () => onSubmit(JSON.stringify({ commands, undoCount, resetCount, runCount, wrongSteps: 0, planningTimeMs: Math.max(1, Math.round(performance.now() - planningStartedAt.current)) }))
  void submit
  return <section className="player" data-testid="planning-player" data-question-id={question.id}><p>{question.instruction}</p><h2>{question.questionText}</h2><p aria-live="polite">Posisi: {position[0] + 1},{position[1] + 1}{position[0] === goal[0] && position[1] === goal[1] ? ' — Sampai!' : ''}</p><div className="options">{['Up', 'Down', 'Left', 'Right'].map(c => <button key={c} disabled={submitting} onClick={() => setCommands(v => [...v, c])}>{({ Up: 'Atas', Down: 'Bawah', Left: 'Kiri', Right: 'Kanan' } as Record<string, string>)[c]}</button>)}</div><p>Langkah: {commands.join(' → ') || 'belum ada'}</p><button disabled={!commands.length || submitting} onClick={() => setCommands(v => v.slice(0, -1))}>Undo</button><button disabled={submitting} onClick={() => { setCommands([]); setPosition(start) }}>Reset</button><button disabled={!commands.length || submitting} onClick={run}>Jalankan</button><button disabled={submitting} onClick={() => onSubmit(JSON.stringify(commands))}>Kirim</button></section>
}

void PlanningQuestionLegacy

function AttentionQuestion({ question, submitting, onSubmit }: { question: Question; submitting: boolean; onSubmit: (answer: string) => Promise<void> }) {
  const stimulus = JSON.parse(question.stimulusJson ?? '{}') as { target?: { display?: string } | string; items?: ({ id?: string; display?: string } | string)[] }
  const [selected, setSelected] = useState<string[]>([])
  const items = stimulus.items ?? []
  const toggle = (index: string) => setSelected(current => current.includes(index) ? current.filter(value => value !== index) : [...current, index])
  const display=(item: {display?:string}|string)=>typeof item==='string'?item:item.display ?? '•'; const id=(item: {id?:string}|string,index:number)=>typeof item==='string'?String(index):item.id ?? String(index); const target=typeof stimulus.target==='string'?stimulus.target:stimulus.target?.display
  return <section className="player" data-testid={`attention-${question.questionType === 'TargetDetection' ? 'target' : 'search'}`} data-question-id={question.id}><p>{question.instruction}</p><h2>{question.questionText}</h2><p aria-label="Target">Contoh: <span style={{fontSize:'2rem'}}>{target}</span></p><div className="memory-grid">{items.map((item, index) => <button key={id(item,index)} aria-pressed={selected.includes(id(item,index))} disabled={submitting} onClick={() => toggle(id(item,index))}>{display(item)}</button>)}</div><button disabled={submitting} onClick={() => onSubmit(JSON.stringify(selected))}>Kirim</button></section>
}

function StandardQuestion({ question, disabled, onSubmit }: { question: Question; disabled: boolean; onSubmit: (answer: string) => Promise<void> }) {
  return <section className="player" data-testid="standard-question" data-question-id={question.id}><p>{question.instruction}</p><h2>{question.questionText}</h2><div className="options">{question.options.map(option => <button key={option.code} disabled={disabled} onClick={() => onSubmit(option.code)}>{option.text}</button>)}</div></section>
}

function MemoryQuestion({ question, onSubmit }: { question: Question; onSubmit: (answer: string, responseStartedAt: number) => Promise<void> }) {
  const [phase, setPhase] = useState<'presenting' | 'retentionGap' | 'responding'>('presenting')
  const [shownItem, setShownItem] = useState('')
  const [response, setResponse] = useState<string[]>([])
  const [responseCandidates, setResponseCandidates] = useState<string[]>([])
  const [submitting, setSubmitting] = useState(false)
  const responseStartedAt = useRef(0)
  const stimulus = JSON.parse(question.stimulusJson ?? '{}') as { sequence?: string[]; candidatePool?: string[]; presentationDurationMs?: number; retentionGapMs?: number }
  const sequence = stimulus.sequence ?? []
  const isSpatial = question.questionType === 'SpatialMemory'
  const isAuditory = question.questionType === 'AuditorySequence' || question.questionType === 'InstructionSequence'

  useEffect(() => {
    const timers: number[] = []
    setPhase('presenting')
    setResponse([])
    // Layout is independent of the answer sequence. Spatial locations deliberately remain fixed.
    setResponseCandidates(isSpatial ? [] : [...new Set(stimulus.candidatePool ?? sequence)].sort(() => Math.random() - 0.5))
    setSubmitting(false)
    responseStartedAt.current = 0
    let index = 0
    const beginResponse = () => {
      setShownItem('')
      setPhase('retentionGap')
      timers.push(window.setTimeout(() => { setPhase('responding'); responseStartedAt.current = performance.now() }, memoryDelay(stimulus.retentionGapMs, 900)))
    }
    const showNext = () => {
      const item = sequence[index] ?? ''
      setShownItem(item)
      if (isAuditory && 'speechSynthesis' in window && item) {
        window.speechSynthesis.cancel()
        const utterance = new SpeechSynthesisUtterance(item)
        utterance.lang = 'id-ID'
        utterance.onend = () => {
          index += 1
          if (index < sequence.length) showNext()
          else beginResponse()
        }
        window.speechSynthesis.speak(utterance)
        return
      }
      index += 1
      if (index < sequence.length) timers.push(window.setTimeout(showNext, memoryDelay(stimulus.presentationDurationMs, 900)))
      else timers.push(window.setTimeout(beginResponse, memoryDelay(stimulus.presentationDurationMs, 900)))
    }
    showNext()
    return () => { timers.forEach(window.clearTimeout); window.speechSynthesis?.cancel() }
  }, [question.id])

  const choose = (item: string) => setResponse(current => [...current, item])
  const cells = Array.from({ length: 9 }, (_, index) => `${Math.floor(index / 3)},${index % 3}`)
  return <section className="player" data-testid={`memory-${isSpatial ? 'spatial' : isAuditory ? 'auditory' : 'sequence'}`} data-question-id={question.id} data-question-title={question.questionText}><p>{question.instruction}</p><h2>{phase === 'presenting' ? (isAuditory ? 'Dengarkan…' : shownItem) : phase === 'retentionGap' ? 'Tunggu sebentar…' : isSpatial ? 'Pilih kotak sesuai urutan' : 'Susun ulang urutan'}</h2>{phase === 'presenting' && isSpatial && <div className="memory-grid" data-testid="spatial-grid">{cells.map(cell => <div key={cell} data-active={cell === shownItem}>{cell === shownItem ? '●' : ''}</div>)}</div>}{phase === 'responding' && <><p>Jawaban: {response.join(' → ') || '—'}</p>{isSpatial ? <div className="memory-grid" data-testid="spatial-response">{cells.map(cell => <button key={cell} disabled={submitting} onClick={() => choose(cell)} aria-label={`Kotak ${cell}`}/>)}</div> : <div className="options">{responseCandidates.map(item => <button key={item} disabled={submitting} onClick={() => choose(item)}>{item}</button>)}</div>}<button disabled={!response.length || submitting} onClick={() => setResponse(current => current.slice(0, -1))}>Undo</button><button disabled={submitting} onClick={async () => { setSubmitting(true); await onSubmit(JSON.stringify(response), responseStartedAt.current) }}>Kirim</button></>}</section>
}
