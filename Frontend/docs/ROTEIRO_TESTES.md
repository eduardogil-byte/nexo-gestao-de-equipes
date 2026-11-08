# Nexo — Roteiro de testes manuais no SENAI

**Objetivo:** validar navegação, permissões, integração com SQL Server e principais fluxos; ainda não substitui testes automatizados.

## 1. Preparação

- [ ] SQL Server ativo e conexão do backend configurada.
- [ ] Backend .NET 8 iniciado em `http://localhost:5088`.
- [ ] `npm install`, `npm run typecheck` e `npm run build` sem erros.
- [ ] `npm run test:api` responde `status: ok`.
- [ ] Frontend aberto em `http://localhost:5173`.

## 2. Cadastro e aprovação

- [ ] Criar usuário no frontend e verificar mensagem de aprovação pendente.
- [ ] Tentar entrar com conta pendente: o backend nega acesso (403).
- [ ] Promover primeiro administrador pelo script do backend.
- [ ] Entrar como administrador, aprovar uma segunda conta em Administração.
- [ ] Entrar como integrante: menu Administração não aparece.
- [ ] Rejeitar uma conta de teste e confirmar impossibilidade de login.

## 3. Projetos

- [ ] Criar projeto privado, definindo um líder aprovado.
- [ ] Projeto aparece como `Proposto`.
- [ ] Adicionar integrante e criar tarefa como integrante: estado passa para `Em andamento`.
- [ ] Alterar informações do projeto como líder e verificar atualização.
- [ ] Usuário externo não consegue abrir projeto privado.
- [ ] Projeto visível permite consulta por usuário aprovado que não está na equipe, sem ações de edição.
- [ ] Administrador conclui, arquiva e reabre projeto; as tarefas/histórico permanecem.

## 4. Tarefas e equipe

- [ ] Integrante cria tarefa própria com prioridade e prazo opcional.
- [ ] Líder atribui tarefa a integrante (aceite automático).
- [ ] Integrante propõe tarefa a colega, que precisa aceitar.
- [ ] Tarefa com dois responsáveis: concluir só uma parte, depois finalizar a tarefa.
- [ ] Líder reabre uma tarefa concluída.
- [ ] Editar título, descrição, prioridade e prazo.
- [ ] Registrar comentário e consultar novamente.
- [ ] Remover integrante: suas tarefas ficam para redistribuição.

## 5. Atividades e pesquisa

- [ ] Definir jornada semanal em Meu perfil.
- [ ] Registrar atividade geral sem projeto.
- [ ] Registrar atividade vinculada a projeto e tarefa.
- [ ] Registrar dia sem atividades; confirmar que não é possível coexistir com registro de trabalho no mesmo dia.
- [ ] Registrar atividade retroativa e verificar indicação no histórico.
- [ ] Editar atividade e confirmar atualização.
- [ ] Adicionar obstáculo e resolver com descrição de solução.
- [ ] Criar etapa opcional e concluí-la.
- [ ] Cadastrar entrega e marcá-la como concluída.
- [ ] Enviar arquivo permitido menor que 10 MB e baixar com autenticação.

## 6. Colaborações externas e dashboards

- [ ] Registrar contribuição externa para projeto público ou por código privado.
- [ ] Líder confirma a contribuição pendente.
- [ ] Verificar os 3 indicadores independentes no detalhe do projeto.
- [ ] Verificar alertas e notificações; marcar como lidas.
- [ ] Verificar o dashboard com usuário comum e como administrador.

## 7. Robustez

- [ ] Desligar backend e tentar consultar uma tela: exibir mensagem compreensível.
- [ ] Abrir o sistema em tela pequena: menu e formulários continuam navegáveis.
- [ ] Verificar que botões administrativos não aparecem para usuário comum.
- [ ] Tentar diretamente a API com token comum em rota administrativa: receber 403.
- [ ] Confirmar que não existe senha de SQL Server, JWT fixo ou usuário padrão no frontend.

## Critérios de conclusão

Somente após **`npm run build`** sem erros e verificação dos fluxos contra um **SQL Server real** considerar o frontend validado para demonstração no laboratório.
