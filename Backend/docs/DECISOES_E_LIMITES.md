# Decisões arquiteturais e pontos de atenção

## Motivações

- **Monólito modular:** aplicação de uma API, com entidades, infraestrutura e endpoints organizados por área. Adequado para equipe pequena e menos de 100 usuários.
- **EF Core 8 + SQL Server:** integração com a stack solicitada, com ajuste de compatibility level para cenário de laboratório.
- **Identity + JWT:** usuários com senha protegida e papéis gerais, liderança por relação com projeto.
- **Aprovação manual:** novo cadastro fica pendente e não entra antes de aprovação administrativa.
- **Rastreamento:** eventos relevantes salvos em `AuditEvents` com autor/data, sem apagar histórico de projetos e tarefas.
- **Progresso:** tarefas (%), etapas (%) e avaliação do líder (%) independentes; sem pontuação de ranking.
- **Colaboração externa:** acesso por código imprevisível de 32 caracteres, sem abrir projeto privado para leitura.
- **Sem controle de ponto:** a jornada configura expectativa de registros, não valida presença, faltas, férias ou folha.

## Limitações deliberadas para o piloto

1. O banco de testes é criado com `EnsureCreated` e **não inclui migrations versionadas**. É necessário substituir esse mecanismo antes de produção.
2. Controle de acesso auditável com `Admin`/`Member`; permissões finas e fluxos de cadastro/perfil podem ser refinados após validação no SENAI.
3. E-mail, MFA, confirmação de e-mail e mecanismos de recuperação de senha **ainda não estão implementados**.
4. Armazenamento físico local não serve para hospedagem efêmera; para deploy configure armazenamento persistente, políticas de retenção, scanning de malware e backup.
5. Notificações de prazo são geradas quando a API está rodando, verificadas a cada seis horas, baseadas em **UTC**. Não equivale a serviço de agendamento externo resiliente.
6. Nenhuma migration / compilação / teste de integração real foi executado neste ambiente de geração. É obrigatório executar `dotnet build`, `dotnet test` e `testar-api.ps1` em PC com .NET + SQL.
7. Regras de calendário letivo, feriados, férias e justificativas de ausência não fazem parte deste piloto.
8. Sem testes completos de concorrência e transações envolvendo várias operações de negócio. O `rowversion` ajuda a detectar conflito de atualização, porém regras de confirmação de tarefa merecem testes concorrentes específicos.
9. O valor do orçamento é informação de projeto, sem rotinas financeiras.
10. HTTP local é apenas modo de laboratório. Para acesso pela internet: HTTPS, segredos seguros, banco atualizado, logs protegidos, backups, políticas de privacidade e atualizações de segurança.

## Próximos passos profissionais

- Confirmar com a coordenação campos, conceitos de progresso, política de dados e autorização entre projetos.
- Criar migrations gerenciadas e testes de integração EF Core com SQL Server.
- Externalizar arquivos de forma persistente.
- Implementar versão suportada do .NET (10 LTS), caso a infraestrutura permita.
- Adicionar pipeline de CI (build, testes, análise de segurança) e revisar endpoints com teste multiusuário.
- Implementar frontend React/TypeScript/Tailwind com CORS configurado por ambiente.
