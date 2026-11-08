/** @type {import('tailwindcss').Config} */
export default { content: ['./index.html','./src/**/*.{ts,tsx}'], theme: { extend: { colors: { nexo: { navy:'#0F172A',blue:'#2563EB',teal:'#0D9488',bg:'#F8FAFC',border:'#E2E8F0' } }, fontFamily: { sans: ['Inter','Segoe UI','system-ui','sans-serif'] }, boxShadow: { card:'0 4px 20px rgba(15,23,42,.04)' } } }, plugins: [] };
