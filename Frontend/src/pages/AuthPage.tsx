import { useState, type FormEvent } from 'react';
import { ArrowRight, CheckCircle2, Eye, EyeOff, LockKeyhole, ShieldCheck } from 'lucide-react';
import { Brand, ErrorMessage } from '../components/UI';
import { useAuth } from '../contexts/AuthContext';
export default function AuthPage() {
  const { login, register } = useAuth();
  const [registering, setRegistering] = useState(false);
  const [visible, setVisible] = useState(false);
  const [fullName, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [specialty, setSpecialty] = useState('');
  const [error, setError] = useState<unknown>();
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess('');
    setBusy(true);
    try {
      if (registering) {
        await register(fullName, email, password, specialty);
        setSuccess('Cadastro enviado! Aguarde a aprovação da coordenação antes de entrar.');
        setRegistering(false);
        setPassword('');
      } else {
        await login(email, password);
      }
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="relative hidden flex-col justify-between overflow-hidden bg-nexo-navy p-12 text-white lg:flex">
        <div className="absolute -left-40 bottom-[-200px] h-[600px] w-[600px] rounded-full bg-blue-600/10 blur-3xl" />
        <div className="absolute right-[-180px] top-[-140px] h-[540px] w-[540px] rounded-full bg-teal-500/10 blur-3xl" />
        <div className="relative">
          <Brand light />
        </div>
        <div className="relative max-w-lg">
          <div className="mb-7 inline-flex items-center gap-2 rounded-full border border-blue-300/15 bg-blue-500/10 px-4 py-2 text-xs font-semibold text-blue-200">
            <ShieldCheck size={14} /> Gestão integrada de pesquisa
          </div>
          <h1 className="text-5xl font-extrabold leading-[1.15] tracking-tight">
            Projetos conectados.
            <br />
            <span className="text-blue-400">Resultados visíveis.</span>
          </h1>
          <p className="mt-6 text-lg leading-relaxed text-slate-400">
            Organize equipes, acompanhe tarefas e registre a evolução dos projetos de pesquisa em um
            único lugar.
          </p>
          <div className="mt-10 grid grid-cols-2 gap-4">
            <div className="rounded-xl border border-slate-700 bg-white/5 p-5">
              <CheckCircle2 className="mb-3 text-teal-400" />
              <div className="text-sm font-semibold">Acompanhamento claro</div>
              <div className="mt-1 text-xs text-slate-400">Atividades e entregas em tempo real</div>
            </div>
            <div className="rounded-xl border border-slate-700 bg-white/5 p-5">
              <LockKeyhole className="mb-3 text-blue-400" />
              <div className="text-sm font-semibold">Acesso controlado</div>
              <div className="mt-1 text-xs text-slate-400">Dados protegidos por projeto</div>
            </div>
          </div>
        </div>
        <div className="relative text-xs text-slate-500">
          Nexo · Conexão entre pessoas, projetos e áreas
        </div>
      </div>
      <div className="flex items-center justify-center bg-nexo-bg px-5 py-12">
        <div className="w-full max-w-[430px]">
          <div className="mb-10 lg:hidden">
            <Brand />
          </div>
          <p className="mb-2 text-xs font-bold uppercase tracking-[.2em] text-nexo-teal">
            Bem-vindo ao Nexo
          </p>
          <h2 className="text-3xl font-extrabold tracking-tight">
            {registering ? 'Criar minha conta' : 'Acessar plataforma'}
          </h2>
          <p className="mt-2 text-sm text-slate-500">
            {registering
              ? 'Seu cadastro será analisado pela coordenação.'
              : 'Entre para acompanhar seus projetos e atividades.'}
          </p>
          <form onSubmit={submit} className="mt-8 space-y-4">
            {registering && (
              <>
                <label className="block">
                  <span className="field-label">Nome completo</span>
                  <input
                    className="field"
                    value={fullName}
                    onChange={(e) => setName(e.target.value)}
                    required
                    minLength={3}
                    maxLength={150}
                    autoComplete="name"
                  />
                </label>
                <label className="block">
                  <span className="field-label">Área de atuação (opcional)</span>
                  <input
                    className="field"
                    value={specialty}
                    onChange={(e) => setSpecialty(e.target.value)}
                    maxLength={120}
                    placeholder="Ex.: Eletrônica, Desenvolvimento..."
                  />
                </label>
              </>
            )}
            <label className="block">
              <span className="field-label">E-mail</span>
              <input
                className="field"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="voce@instituicao.com"
              />
            </label>
            <label className="block">
              <span className="field-label">Senha</span>
              <div className="relative">
                <input
                  className="field pr-12"
                  type={visible ? 'text' : 'password'}
                  required
                  minLength={registering ? 10 : 1}
                  autoComplete={registering ? 'new-password' : 'current-password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                />
                <button
                  type="button"
                  aria-label={visible ? 'Ocultar senha' : 'Mostrar senha'}
                  className="absolute right-3 top-3 text-slate-400"
                  onClick={() => setVisible(!visible)}
                >
                  {visible ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
              {registering && (
                <p className="mt-1.5 text-xs text-slate-500">
                  Mínimo de 10 caracteres, com maiúscula, número e símbolo.
                </p>
              )}
            </label>
            <ErrorMessage error={error} />
            {success && (
              <div
                role="status"
                className="rounded-xl border border-teal-200 bg-teal-50 p-3 text-sm text-teal-800"
              >
                {success}
              </div>
            )}
            <button className="btn-primary w-full py-3" disabled={busy}>
              {busy ? 'Aguarde...' : registering ? 'Solicitar acesso' : 'Entrar no Nexo'}
              <ArrowRight size={17} />
            </button>
          </form>
          <div className="mt-7 text-center text-sm text-slate-500">
            {registering ? 'Já possui uma conta?' : 'Ainda não tem acesso?'}{' '}
            <button
              className="font-semibold text-nexo-blue hover:underline"
              onClick={() => {
                setRegistering(!registering);
                setError(null);
                setSuccess('');
              }}
            >
              {registering ? 'Entrar' : 'Criar conta'}
            </button>
          </div>
          <div className="mt-14 border-t border-slate-200 pt-5 text-center text-xs text-slate-400">
            Acesso sujeito à aprovação da coordenação.
          </div>
        </div>
      </div>
    </div>
  );
}
