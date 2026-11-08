const target = process.env.VITE_BACKEND_URL || 'http://localhost:5088';
try { const response = await fetch(`${target.replace(/\/$/,'')}/api/health`, {signal:AbortSignal.timeout(5000)}); const body = await response.text(); if(!response.ok)throw Error(`HTTP ${response.status}: ${body}`); console.log(`OK — ${target}/api/health`);console.log(body);}
catch(err){console.error('API indisponível. Inicie primeiro o backend .NET 8 em http://localhost:5088.');console.error(err.message);process.exitCode=1;}
