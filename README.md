# 🛡️ Anti-Peek

Utilitário ultraleve e portátil para **Windows** (~12 KB) que aplica **camuflagem de opacidade (transparência)** em qualquer janela ativa através do atalho **`Ctrl + Shift + F10`**, protegendo dados privados e sensíveis contra olhares curiosos (*shoulder surfing*).

Inspirado no funcionamento interno do recurso *Secret Screen* (`Fn + F10` dos notebooks Samsung), mas desbloqueado para funcionar em **qualquer computador ou notebook com Windows 10/11**, sem precisar instalar nada.

---

## ✨ Recursos

- **Atalho Global (`Ctrl + Shift + F10`):** Alterna instantaneamente a janela em foco entre o modo camuflado (transparente) e 100% visível.
- **Proteção da Área de Trabalho:** Ignora automaticamente a Área de Trabalho (`Progman`/`WorkerW`) e a Barra de Tarefas (`Shell_TrayWnd`), evitando deixar o próprio Windows invisível por engano.
- **Ícone na Bandeja do Sistema (System Tray):**
  - Ajuste rápido do nível de opacidade (**10%**, **18% [Padrão]**, **30%** ou **50%**).
  - **Restaurar Todas as Janelas** (também acessível dando duplo clique no ícone da bandeja).
  - Opção **"Iniciar junto com o Windows"** (via registro do usuário atual, sem exigir permissão de Administrador).
  - Restaura automaticamente todas as janelas camufladas ao fechar o programa.
- **Zero Dependências Externas:** Executável único (`.exe`), sem instalador e sem arquivos externos.

---

## 🚀 Como Usar no Windows

1. Baixe ou execute diretamente o arquivo **`AntiPeek.exe`**.
2. Um ícone de escudo aparecerá na bandeja do sistema (perto do relógio).
3. Com qualquer janela aberta em foco, pressione **`Ctrl + Shift + F10`** para camuflar ou restaurar a opacidade da janela.

---

## 🛠️ Como Compilar do Código-Fonte (Sem instalar nada)

Todo Windows 10 e 11 já possui o compilador oficial de C# (`csc.exe`) instalado nativamente. Para recompilar o `AntiPeek.exe` após editar o `AntiPeek.cs`, abra o PowerShell na pasta do projeto e execute:

```powershell
.\build.ps1
```

---

## 🐧 Dica Equivalente para Linux (Debian / Ubuntu)

No Linux com servidor gráfico X11, esse mesmo comportamento já está disponível nativamente no pacote oficial `x11-apps` através do utilitário `transset`:

```bash
sudo apt update && sudo apt install x11-apps
```

Depois, basta cadastrar o atalho **`Ctrl + Shift + F10`** nas configurações de teclado do seu ambiente gráfico executando o comando:

```bash
transset --actual --toggle 0.18
```
