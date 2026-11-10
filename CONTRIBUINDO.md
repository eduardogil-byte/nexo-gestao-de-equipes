# Como contribuir com o Nexo

Somos três pessoas no mesmo repositório. Para ninguém sobrescrever o trabalho do outro, todo
mundo segue o mesmo fluxo.

## Regras de ouro

1. **Nunca trabalhe direto na `main`.** Ela deve sempre estar funcionando.
2. **Cada um commita com a própria conta** do Git/GitHub (confira com `git config user.name` e
   `git config user.email`). Assim o histórico mostra quem fez o quê.
3. **Todo código entra na `main` por pull request (PR)**, revisado por pelo menos mais uma pessoa.
4. **Nunca commite segredos:** senhas, connection strings com senha, chaves JWT, arquivos `.env`.

## Fluxo passo a passo

```bash
# 1. Atualize a main local
git checkout main
git pull

# 2. Crie sua branch a partir da main
git checkout -b frontend/tela-de-tarefas

# 3. Trabalhe e faça commits pequenos
git add arquivo1 arquivo2
git commit -m "Adiciona filtro por situação na lista de tarefas"

# 4. Envie a branch para o GitHub
git push -u origin frontend/tela-de-tarefas

# 5. Abra o pull request no GitHub, descreva o que mudou e peça revisão
# 6. Depois do merge, volte para a main e apague a branch antiga
git checkout main
git pull
git branch -d frontend/tela-de-tarefas
```

Se a `main` andou enquanto você trabalhava, traga as novidades para a sua branch antes do PR:

```bash
git checkout main && git pull
git checkout minha-branch
git merge main
```

## Nomes de branch

Use `area/descricao-curta`, em minúsculas e com hífens:

| Prefixo      | Para quê                          | Exemplo                          |
| ------------ | --------------------------------- | -------------------------------- |
| `backend/`   | API, regras de negócio, endpoints | `backend/endpoint-de-tarefas`    |
| `frontend/`  | Telas e componentes React         | `frontend/pagina-do-dashboard`   |
| `banco/`     | Entidades, DbContext, SQL         | `banco/indice-em-projetos`       |
| `testes/`    | Testes automatizados              | `testes/concluir-tarefa`         |
| `docs/`      | README, checklists, apresentação  | `docs/roteiro-da-demo`           |

## Boas mensagens de commit

- Escreva em **português**, no **imperativo**, começando com verbo: *Adiciona*, *Corrige*,
  *Remove*, *Atualiza*, *Renomeia*.
- A primeira linha diz **o que** mudou, em até ~70 caracteres, sem ponto final.
- Se precisar explicar o **porquê**, deixe uma linha em branco e escreva um parágrafo.
- **Um assunto por commit.** Se você usou "e" duas vezes na mensagem, provavelmente são dois commits.

| Ruim                | Bom                                                           |
| ------------------- | ------------------------------------------------------------- |
| `ajustes`           | `Corrige cálculo de progresso quando não há tarefas`          |
| `mudei umas coisas` | `Adiciona validação de e-mail duplicado no cadastro`          |
| `final v2`          | `Atualiza README com o fluxo da demonstração`                 |

## Pull requests

- Título claro, como uma mensagem de commit.
- Descreva **o que mudou**, **por que** e **como testar**.
- Mantenha o PR pequeno: é mais fácil de revisar.
- Antes de abrir, rode o que for da sua área:
  - Backend: `dotnet build` e `dotnet test` em `Backend/`
  - Frontend: `npm run format:check`, `npm run typecheck` e `npm run build` em `Frontend/`
- Quem revisa comenta, pede ajustes ou aprova; quem abriu o PR não aprova o próprio.

## Formatação do frontend

O frontend usa Prettier. Antes de commitar, rode em `Frontend/`:

```bash
npm run format
```
