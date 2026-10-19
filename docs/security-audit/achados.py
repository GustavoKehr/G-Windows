# -*- coding: utf-8 -*-
"""Dados da auditoria (achados, pontos fortes, cobertura por categoria, recomendações, issues).
Tudo aqui foi verificado no código real em 18/10/2026; as linhas citadas referem-se ao estado do repositório nessa data."""

PROJETO = "G-Windows"

SEV_ORDEM = ["Crítica", "Alta", "Média", "Baixa", "Informativa"]
SEV_COR = {"Crítica": "#B91C1C", "Alta": "#EA580C", "Média": "#D97706", "Baixa": "#2563EB", "Informativa": "#6B7280"}
COR_FORTE = "#059669"

CATEGORIAS = [
    "Secrets expostos no frontend", "Validação insuficiente no frontend", "Validação insuficiente no backend",
    "SQL Injection", "Prompt Injection", "Cross-Site Scripting (XSS)", "Autenticação fraca", "Exposição IDOR/BOLA",
    "Senhas armazenadas em texto puro", "Ataque de força bruta", "CSRF", "Upload sem validação",
    "Mensagens de erro que vazam informações", "Tokens mal otimizados", "Rate limit insuficiente",
    "Dados sensíveis expostos", "SSRF", "Cookies inseguros", "CORS mal configurado",
    "Abuso de requisições (DoS/DDoS)", "Enumeração de recursos", "Exposição de rotas administrativas",
]
OUTROS = "Outros (fora da checklist)"

# status: "achado" | "correto" | "parcial" | "na"
COBERTURA = {
    1: ("correto", "Busca por password|secret|token|api key|http(s)|Http* em src/ (*.cs, *.xaml, *.csproj) não retorna segredo algum; a única URL do repositório é o feed nuget.org em nuget.config (build, não runtime)."),
    2: ("parcial", "Colar caminhos valida existência antes de aceitar (FenceWindow.cs:238-241). Já a regra de captura e o caminho de portal digitados em Prompt não são validados (ver A-03 e A-01)."),
    3: ("achado", "Não há backend. O equivalente é o layout.json/importação, entrada não confiável aceita sem validação de schema, nulos ou faixas (A-02)."),
    4: ("na", "Sem banco de dados nem consultas SQL (busca por 'Sql' sem ocorrências)."),
    5: ("na", "Sem integração com LLM; nenhum texto de usuário é enviado a modelo."),
    6: ("na", "Aplicação WPF sem WebBrowser/WebView; todo texto é renderizado por TextBlock.Text (sem interpretação de HTML/markup)."),
    7: ("na", "Sem autenticação: aplicativo local de usuário único."),
    8: ("na", "Sem recursos de múltiplos usuários nem identificadores de objeto expostos."),
    9: ("na", "O aplicativo não armazena senhas nem credenciais."),
    10: ("na", "Sem formulário de login ou endpoint a ser atacado."),
    11: ("na", "Sem requisições HTTP nem sessões de navegador."),
    12: ("achado", "Equivalente: importação de layout e restauração de snapshot (App.xaml.cs:213, 223) aceitam arquivo JSON externo sem validação (A-01)."),
    13: ("correto", "Nenhuma mensagem de erro com caminho, pilha ou dado interno é exibida: exceções são capturadas e descartadas (ex.: FenceWindow.cs:255, Palette.cs:54)."),
    14: ("na", "Sem tokens de sessão/API."),
    15: ("na", "Sem endpoints; nada a limitar por taxa."),
    16: ("achado", "O widget Notas grava texto livre em claro em layout.json, em snapshots e na exportação (A-05). Sem telemetria nem envio de dados."),
    17: ("achado", "Sem requisições HTTP. O análogo local é o acesso automático a caminhos UNC vindos de layout importado (A-01)."),
    18: ("na", "Sem cookies."),
    19: ("na", "Sem servidor HTTP/CORS."),
    20: ("achado", "DoS local: regex sem limite de tempo na regra de captura (A-03) e leitura de linha inteira na pré-visualização (A-04)."),
    21: ("na", "Sem recursos endereçáveis por rede."),
    22: ("na", "Sem rotas. O processo roda sem elevação (app.manifest não pede requireAdministrator)."),
}

ACHADOS = [
    {
        "id": "A-01", "status": ("Mitigado", "Caminhos de rede continuam permitidos por decisão do usuário. O layout agora é normalizado ao carregar e a importação/restauração pede confirmação quando o arquivo traz caminhos de rede (App.xaml.cs, ImportFrom)."), "sev": "Média", "cat": 12, "extra_cat": [17],
        "titulo": "Layout importado ou restaurado não é validado: caminhos UNC/remotos são acessados e executados com 1 clique",
        "locais": [
            ("src/GWindows.App/App.xaml.cs", "213", "snaps.DropDownItems.Add(f.Name[..^5], null, (_, _) => { Snapshot(); Reload(LayoutStore.Load(f.FullName).Fences); });"),
            ("src/GWindows.App/App.xaml.cs", "223", "if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) { Snapshot(); Reload(LayoutStore.Load(d.FileName).Fences); }"),
            ("src/GWindows.Core/Layout.cs", "49", "try { if (File.Exists(p)) return JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(p)) ?? new(); }"),
            ("src/GWindows.App/FenceWindow.cs", "271, 279", "return _d.Items.Where(p => File.Exists(p) || Directory.Exists(p));   // e Directory.EnumerateFileSystemEntries(_d.Portal) (l.271)"),
            ("src/GWindows.App/FenceWindow.cs", "395", "using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path);"),
            ("src/GWindows.App/FenceWindow.cs", "255, 351", "static void Launch(string p) { ... Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }) ... }   //   if (click) Launch(path);"),
            ("src/GWindows.App/Palette.cs", "54", "() => { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception) { } }"),
        ],
        "porque": "O JSON importado define Items e Portal livremente. Ao abrir cada cerca, o aplicativo consulta File.Exists/Directory.Exists, enumera o portal, observa a pasta com FileSystemWatcher e extrai o ícone do arquivo; se o caminho for \\\\servidor\\share\\x.exe, o Windows contacta o servidor SMB do atacante (tentativa de autenticação NTLM, com vazamento do hash). O rótulo vem do nome do arquivo e o ícone do próprio executável remoto, então o atacante controla a aparência (p.ex. um falso 'Chrome'). Como o item agora abre com um clique, um clique executa o programa remoto.",
        "condicoes": "Exige que o usuário importe um .json de origem não confiável (ou restaure um snapshot adulterado) e depois clique no item. O Windows pode exibir aviso de segurança para executáveis de rede; o vazamento de NTLM ocorre antes de qualquer clique, no carregamento. Confirmado por leitura do código e por teste com o carregador real: o arquivo com Portal e Items UNC foi aceito sem alteração (saída do teste: Portal=\\\\attacker.example\\share). O comportamento SMB do Windows não foi exercitado (sem servidor de teste).",
        "impacto": "Vazamento de credenciais NTLM, execução de código remoto com interação do usuário e aparência de atalho falsificada.",
        "correcao": "Validar o layout importado: aceitar só caminhos totalmente qualificados em unidades locais, rejeitar UNC (\\\\) e prefixos de dispositivo (\\\\?\\, \\\\.\\); limitar tamanho do arquivo; exigir confirmação mostrando quantos itens/caminhos externos o arquivo traz; não resolver (File.Exists) caminhos de rede sem consentimento.",
    },
    {
        "id": "A-02", "status": ("Corrigido", "LayoutStore.Normalize remove nulos e limita tamanhos/contagens/faixas; qualquer falha de leitura cai para o .bak e depois para o padrão; arquivo acima de 20 MB é ignorado (Layout.cs). Teste automatizado cobre o caso."), "sev": "Baixa", "cat": 3, "extra_cat": [],
        "titulo": "layout.json sem validação de schema, nulos ou faixas: falha na inicialização e valores extremos aceitos",
        "locais": [
            ("src/GWindows.Core/Layout.cs", "49-50", "try { if (File.Exists(p)) return JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(p)) ?? new(); }\ncatch (JsonException) { }"),
            ("src/GWindows.App/App.xaml.cs", "93-95", "if (Layout.Fences.Count == 0) ...;  foreach (var f in Layout.Fences) Fit(f);  foreach (var f in Layout.Fences.ToList()) Open(f);"),
            ("src/GWindows.App/FenceWindow.cs", "267, 271, 279", "bool Manual => _d.Portal.Length == 0 && _d.Smart.Length == 0 && _d.Kind.Length == 0;   // e _d.Items.Where(...) / _d.Portal.Length"),
            ("src/GWindows.Core/Rules.cs", "21", "fences.Any(f => f.Items.Contains(path)) ? null : fences.FirstOrDefault(f => f.Rule.Length > 0 && Matches(f.Rule, path));"),
        ],
        "porque": "Um JSON sintaticamente válido com \"Fences\": null, \"Items\": null ou \"Name\": null é aceito por LayoutStore.Load (teste executado: Fences is null: True; Items/Name/Excluded null: True) e depois desreferenciado em vários pontos (teste: Rules.Target com Items=null lança NullReferenceException). O fallback para layout.json.bak só cobre JsonException (Layout.cs:50), logo não protege nesse caso. Valores como W=2147483647, H=-5, X=2147483647 também são aceitos sem restrição (teste).",
        "condicoes": "Exige escrever em %AppData%\\G-Windows\\layout.json (mesmo usuário) ou importar um arquivo adulterado. Na importação a falha ocorre em Reload antes de persistir, então não vira laço de falha; com o arquivo principal adulterado a falha se repete a cada inicialização até o arquivo ser removido. A falha de inicialização foi demonstrada no nível de dados e de consumo (Rules.Target); a partida completa do aplicativo com arquivo hostil não foi executada para não derrubar a sessão do usuário.",
        "impacto": "Negação de serviço local do aplicativo (queda na abertura) e janelas com geometria absurda.",
        "correcao": "Após desserializar, normalizar: substituir nulos por valores padrão, limitar W/H/X/Y a faixas razoáveis, limitar quantidade de cercas/itens e tamanho dos textos; capturar qualquer Exception (não só JsonException) e cair para .bak/padrão.",
    },
    {
        "id": "A-03", "status": ("Corrigido", "Regex substituído por casador de curingas iterativo (Rules.cs). O padrão patológico contra nome de 250 caracteres responde em menos de 50 ms (teste automatizado)."), "sev": "Baixa", "cat": 20, "extra_cat": [],
        "titulo": "Regra de captura usa regex sem limite de tempo: backtracking catastrófico trava a interface",
        "locais": [
            ("src/GWindows.Core/Rules.cs", "13-14", "var rx = \"^\" + Regex.Escape(raw).Replace(\"\\\\*\", \".*\").Replace(\"\\\\?\", \".\") + \"$\";\nif (Regex.IsMatch(name, rx, RegexOptions.IgnoreCase)) return true;"),
            ("src/GWindows.App/App.xaml.cs", "126-132", "void On(string p) => Current.Dispatcher.InvokeAsync(() => { if (Rules.Target(Layout.Fences, p) is { } f) ... });  w.Created += ...; w.Renamed += ...;"),
        ],
        "porque": "Cada '*' vira '.*'. Uma regra com vários '*' e um nome de arquivo sem o sufixo esperado provoca backtracking exponencial. Medição com a regra *a*a*a*a*a*a*a*a*c e nome 'a'×N + 'b': N=20 → 19 ms; N=24 → 75 ms; N=26 → 163 ms; N=28 → 330 ms; N=30 → 628 ms (dobra a cada +2 caracteres). Nomes de arquivo chegam a 255 caracteres, então o tempo se torna praticamente ilimitado. A avaliação ocorre na thread de interface (Dispatcher.InvokeAsync) a cada arquivo criado ou renomeado no Desktop.",
        "condicoes": "A regra precisa ter vários curingas: possível por digitação própria ou, mais realista, por layout importado (ver A-01). O gatilho é um arquivo novo no Desktop com muitos caracteres iguais ao do padrão (nomes comuns com muitos 'a' bastam).",
        "impacto": "Congelamento da interface (e das cercas) até encerrar o processo.",
        "correcao": "Trocar o regex por um casador de curingas iterativo (O(n·m)) ou construir o Regex com matchTimeout curto (ex.: 50 ms) e tratar RegexMatchTimeoutException como 'não casa'; limitar a quantidade de '*' por padrão.",
    },
    {
        "id": "A-04", "status": ("Corrigido", "Pré-visualização lê no máximo 4 KB e não decodifica imagens acima de 30 MB (FenceWindow.cs, ReadHead)."), "sev": "Baixa", "cat": 20, "extra_cat": [],
        "titulo": "Pré-visualização lê a linha inteira de arquivos de texto na thread da interface (consumo de memória sem limite)",
        "locais": [
            ("src/GWindows.App/FenceWindow.cs", "377-378", "if (ext is \".txt\" or \".md\" or \".log\" or \".json\" or ...)\n    return string.Join(\"\\n\", File.ReadLines(path).Take(6).Select(l => l.Length > 80 ? l[..80] : l)) + ...;"),
            ("src/GWindows.App/FenceWindow.cs", "342", "tip.Opened += (_, _) => { tip.Content = Preview(path); Raise(tip); };"),
        ],
        "porque": "File.ReadLines carrega cada linha inteira em memória antes do corte para 80 caracteres. Teste com a mesma expressão: arquivo de 300 MB sem quebra de linha → 1055 ms e pico de 2251 MB de working set (≈7× o tamanho do arquivo, em UTF-16). A chamada ocorre ao abrir o tooltip, na thread de interface.",
        "condicoes": "Basta passar o mouse sobre um arquivo .txt/.md/.log/.json/.csv/.xml/.cs gigante de linha única (p.ex. JSON minificado ou log). A cerca inteligente 'Maiores Arquivos' lista justamente arquivos acima de 100 MB, o que torna o caso realista.",
        "impacto": "Travamento de segundos e pico de memória de gigabytes, podendo derrubar o processo.",
        "correcao": "Abrir com FileStream/StreamReader e ler no máximo alguns KB (ex.: 4 KB) ignorando arquivos acima de um limite; fazer a leitura fora da thread de interface.",
    },
    {
        "id": "A-05", "status": ("Aceito", "Sem alteração: depende de o usuário não guardar dados sensíveis nas Notas."), "sev": "Informativa", "cat": 16, "extra_cat": [],
        "titulo": "Texto do widget Notas é gravado em claro e replicado em snapshots e exportações",
        "locais": [
            ("src/GWindows.App/FenceWindow.cs", "296-297", "var box = new TextBox { Text = _d.Notes, ... };\nbox.TextChanged += (_, _) => { _d.Notes = box.Text; App.SaveSoon(); };"),
            ("src/GWindows.Core/Layout.cs", "24", "public string Notes { get; set; } = \"\";"),
            ("src/GWindows.App/App.xaml.cs", "191, 218", "File.Copy(LayoutPath, Path.Combine(SnapDir, ...\".json\"), true);   //   File.Copy(LayoutPath, d.FileName, true);"),
        ],
        "porque": "Qualquer texto digitado nas notas (inclusive senhas ou dados pessoais colados) permanece em layout.json e é copiado para até 50 snapshots (30 dias) e para o arquivo exportado, sem criptografia nem aviso.",
        "condicoes": "Depende do usuário usar o widget Notas para dados sensíveis. Os arquivos ficam no perfil do usuário (permissões herdadas da pasta do perfil).",
        "impacto": "Cópias adicionais de dados sensíveis em disco e em arquivos de exportação compartilháveis.",
        "correcao": "Avisar no widget que o conteúdo não é cifrado; excluir Notes dos snapshots e da exportação por padrão ou proteger com DPAPI (ProtectedData, escopo do usuário).",
    },
    {
        "id": "A-06", "status": ("Corrigido", "Explorer iniciado pelo caminho completo da pasta do Windows e com aspas removidas do argumento (FenceWindow.cs)."), "sev": "Informativa", "cat": "outros", "extra_cat": [],
        "titulo": "Explorer iniciado sem caminho completo e com argumento montado por concatenação",
        "locais": [
            ("src/GWindows.App/FenceWindow.cs", "180", "Add(L.T(\"Mostrar no Explorer\"), () => Process.Start(\"explorer.exe\", $\"/select,\\\"{path}\\\"\"));"),
        ],
        "porque": "Process.Start com nome relativo e UseShellExecute=false usa a ordem de busca do CreateProcess, que inclui primeiro o diretório do aplicativo (%LocalAppData%\\Programs\\G-Windows, gravável pelo usuário). Um explorer.exe plantado ali seria executado. O caminho também é interpolado dentro de aspas na linha de comando.",
        "condicoes": "Exige permissão de escrita na pasta do aplicativo (o mesmo usuário), logo não há elevação de privilégio. Não reproduzido dinamicamente: baseado na ordem de busca documentada do Windows. Nomes de arquivo existentes não podem conter aspas, o que limita o risco do argumento.",
        "impacto": "Execução de binário plantado com os privilégios do próprio usuário (persistência/engano).",
        "correcao": "Usar o caminho completo (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), \"explorer.exe\")) e ProcessStartInfo.ArgumentList.",
    },
    {
        "id": "A-07", "status": ("Parcial", "A segunda instância agora exibe um aviso em vez de encerrar em silêncio; o nome do mutex continua previsível (risco aceito)."), "sev": "Informativa", "cat": "outros", "extra_cat": [],
        "titulo": "Mutex de instância única com nome previsível permite impedir a abertura do aplicativo",
        "locais": [
            ("src/GWindows.App/App.xaml.cs", "89-90", "_single = new Mutex(true, \"GWindows.SingleInstance\", out var first);\nif (!first) { Shutdown(); return; }"),
        ],
        "porque": "Qualquer processo do mesmo usuário que crie antes o mutex 'GWindows.SingleInstance' faz o aplicativo encerrar silenciosamente, sem mensagem.",
        "condicoes": "Exige executar código como o mesmo usuário; sem ganho de privilégio.",
        "impacto": "Negação de serviço local silenciosa na abertura.",
        "correcao": "Incluir identificador do usuário/versão no nome e, ao detectar instância existente, avisar ou ativar a instância atual em vez de encerrar sem aviso.",
    },
    {
        "id": "A-08", "status": ("Aceito", "Sem alteração: binário não assinado, uso pessoal."), "sev": "Informativa", "cat": "outros", "extra_cat": [],
        "titulo": "Início automático aponta para binário não assinado em pasta gravável pelo usuário",
        "locais": [
            ("src/GWindows.App/App.xaml.cs", "15-24", "const string RunKey = @\"Software\\Microsoft\\Windows\\CurrentVersion\\Run\";  ...  k.SetValue(\"G-Windows\", $\"\\\"{Environment.ProcessPath}\\\"\");"),
        ],
        "porque": "O valor da chave Run (HKCU) é corretamente entre aspas, mas o executável publicado em %LocalAppData%\\Programs\\G-Windows não é assinado e a pasta é gravável pelo usuário; quem escrever ali passa a ser executado em cada login.",
        "condicoes": "Mesmo usuário; sem elevação. Prática comum em aplicativos por usuário.",
        "impacto": "Facilita persistência de código no perfil do usuário.",
        "correcao": "Assinar o binário (Authenticode) e, se necessário, instalar em pasta com ACL restrita (ex.: MSIX ou %ProgramFiles%).",
    },
]

PONTOS_FORTES = [
    (1, "Nenhum segredo, credencial ou chave no código", "Busca por password|secret|token|api key e URLs em src/ (*.cs, *.xaml, *.csproj): sem ocorrências; única URL: https://api.nuget.org em nuget.config.", "src/**, nuget.config"),
    (16, "Sem rede e sem telemetria", "Nenhum uso de HttpClient, Socket, WebBrowser ou WebView no código; o aplicativo só lê/escreve arquivos locais e o registro do usuário.", "src/** (busca por Http*, Web*)"),
    (2, "Colar caminhos valida existência", "Cada caminho colado só é adicionado se Directory.Exists/File.Exists for verdadeiro, com remoção de aspas e barra final.", "FenceWindow.cs:236-241"),
    (3, "Desserialização segura de JSON", "System.Text.Json em tipos concretos (LayoutFile/FenceData), sem polimorfismo, sem BinaryFormatter, XmlSerializer ou carregamento dinâmico de assemblies.", "Layout.cs:49; App.xaml.cs:227"),
    (13, "Erros não vazam informações", "Falhas ao abrir itens/ícones são capturadas e descartadas; nenhuma mensagem com caminho, pilha ou detalhe interno é mostrada ao usuário.", "FenceWindow.cs:255, 380, 398; Palette.cs:54"),
    (22, "Executa sem privilégios elevados", "app.manifest não declara requestedExecutionLevel (padrão asInvoker); nenhuma operação exige administrador.", "app.manifest:1-14"),
    ("outros", "Gancho de mouse mínimo", "O WH_MOUSE_LL só trata o clique esquerdo (0x201), lê posição e classe da janela sob o cursor e repassa com CallNextHookEx; não registra teclas nem conteúdo de outras janelas. O atalho global usa RegisterHotKey.", "Native.cs:53-77, 84-91"),
    ("outros", "O aplicativo não apaga nem move arquivos do usuário", "Únicas remoções/movimentações: snapshots dentro da própria pasta (limite de 50/30 dias) e o arquivo temporário da gravação atômica. G-Windows guardam apenas referências.", "App.xaml.cs:192; Layout.cs:62"),
    ("outros", "Gravação atômica com backup", "Escrita em arquivo temporário, File.Replace com .bak; evita corrupção por interrupção.", "Layout.cs:55-63"),
    ("outros", "Dependências sem vulnerabilidades conhecidas", "dotnet list package --vulnerable --include-transitive em 18/10/2026: nenhum pacote vulnerável nos três projetos (único pacote de runtime: System.Drawing.Common 8.0.10).", "GWindows.App.csproj:15"),
    ("outros", "Chave Run entre aspas", "O caminho gravado em HKCU\\...\\Run é envolvido por aspas, evitando sequestro por caminho com espaço sem aspas.", "App.xaml.cs:23"),
]

RECOMENDACOES = [
    ("P1", "Validar layouts importados/restaurados (A-01, A-02)", "Rejeitar caminhos UNC/dispositivo, normalizar nulos e faixas, limitar tamanhos e pedir confirmação ao importar. Corrige a única falha de severidade média e a de falha na inicialização."),
    ("P2", "Substituir o regex da regra de captura (A-03)", "Casador de curingas iterativo ou matchTimeout curto. Evita congelar a interface com uma regra ou arquivo malicioso."),
    ("P2", "Limitar a leitura da pré-visualização (A-04)", "Ler poucos KB com StreamReader, recusar arquivos grandes e sair da thread de interface."),
    ("P3", "Endurecer a execução local (A-06, A-07, A-08)", "Caminho completo do Explorer + ArgumentList; nome de mutex único por usuário com aviso; assinatura do binário."),
    ("P3", "Tratar dados das Notas (A-05)", "Avisar que não são cifradas e excluí-las de snapshots/exportação ou cifrar com DPAPI."),
    ("P4", "Manutenção contínua", "Reexecutar dotnet list package --vulnerable a cada release; considerar packages.lock.json para travar versões transitivas."),
]

ISSUES = [
    {
        "titulo": "[Segurança] Layout importado/restaurado sem validação (caminhos UNC acessados e executados)",
        "labels": "security, severidade:média",
        "descricao": "Importar layout (App.xaml.cs:223) e restaurar snapshot (App.xaml.cs:213) carregam o JSON com LayoutStore.Load e entregam as cercas a Reload sem nenhuma validação. Itens e Portal aceitam qualquer caminho, inclusive \\\\host\\share\\arquivo.exe. Ao abrir a cerca o aplicativo faz File.Exists/Directory.Exists, enumera o portal, observa a pasta e extrai o ícone (FenceWindow.cs:271, 279, 308, 395), o que faz o Windows contatar o servidor SMB (tentativa de autenticação NTLM). O item então abre com um clique (FenceWindow.cs:255, 351).",
        "evidencia": "src/GWindows.App/App.xaml.cs:223\n  Snapshot(); Reload(LayoutStore.Load(d.FileName).Fences);\nsrc/GWindows.Core/Layout.cs:49\n  JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(p))\nsrc/GWindows.App/FenceWindow.cs:279\n  return _d.Items.Where(p => File.Exists(p) || Directory.Exists(p));\nTeste: Portal=\\\\attacker.example\\share e Items[0]=\\\\attacker.example\\share\\x.exe foram aceitos sem alteração.",
        "impacto": "Vazamento de hash NTLM no carregamento, execução remota com 1 clique e atalho falsificado (nome e ícone controlados pelo atacante).",
        "correcao": "Validar o layout após desserializar: aceitar apenas caminhos locais totalmente qualificados; rejeitar UNC e prefixos \\\\?\\ e \\\\.\\; limitar tamanho do arquivo e quantidade de itens; exibir confirmação com o resumo do que será importado; não consultar caminhos de rede sem consentimento.",
        "aceite": [
            "Importar um layout com item ou portal UNC é recusado (ou exige confirmação explícita) e nenhuma conexão SMB é feita ao carregar.",
            "Teste automatizado cobre caminhos UNC, \\\\?\\ e relativos no validador.",
            "Arquivos maiores que o limite definido são rejeitados com mensagem clara.",
            "Restaurar snapshot aplica a mesma validação.",
        ],
    },
    {
        "titulo": "[Segurança] layout.json sem validação de schema/nulos/faixas derruba o aplicativo na abertura",
        "labels": "security, severidade:baixa",
        "descricao": "LayoutStore.Load aceita JSON válido com campos nulos e valores extremos e só trata JsonException (Layout.cs:49-50). Fences/Items/Name nulos causam NullReferenceException em App.xaml.cs:93-95, FenceWindow.cs:267-279 e Rules.cs:21; o fallback .bak não é usado nesse caso.",
        "evidencia": "src/GWindows.Core/Layout.cs:49-50\n  try { ... Deserialize<LayoutFile>(File.ReadAllText(p)) ?? new(); } catch (JsonException) { }\nTestes executados: {\"Fences\":null} -> Fences null; {\"Items\":null,\"Name\":null} aceitos; Rules.Target com Items=null -> NullReferenceException; W=2147483647, H=-5 aceitos.",
        "impacto": "Queda do aplicativo a cada inicialização enquanto o arquivo adulterado existir; janelas com geometria absurda.",
        "correcao": "Normalizar o objeto desserializado (nulos -> padrão, limites de X/Y/W/H, quantidade e tamanho de textos) e capturar qualquer Exception para cair no .bak e depois no layout padrão.",
        "aceite": [
            "Layout com Fences/Items/Name/Excluded nulos abre o aplicativo com valores padrão, sem exceção.",
            "Geometria fora dos limites é ajustada ao carregar.",
            "Falha de leitura (IOException/UnauthorizedAccess) cai no .bak e depois no padrão.",
            "Testes unitários cobrem os três casos acima.",
        ],
    },
    {
        "titulo": "[Segurança] Regra de captura com regex sem timeout (ReDoS) trava a interface",
        "labels": "security, severidade:baixa",
        "descricao": "Rules.Matches converte cada '*' em '.*' e usa Regex.IsMatch sem timeout (Rules.cs:13-14). A chamada ocorre na thread de interface para cada arquivo novo no Desktop (App.xaml.cs:126-132).",
        "evidencia": "src/GWindows.Core/Rules.cs:13-14\n  var rx = \"^\" + Regex.Escape(raw).Replace(\"\\\\*\", \".*\")...;\nMedição (regra *a*a*a*a*a*a*a*a*c, nome 'a'xN+'b'): N=20 19 ms | N=24 75 ms | N=26 163 ms | N=28 330 ms | N=30 628 ms (dobra a cada +2).",
        "impacto": "Congelamento da interface e das cercas por tempo praticamente ilimitado.",
        "correcao": "Casador de curingas iterativo (O(n·m)) ou Regex com matchTimeout curto tratando RegexMatchTimeoutException como 'não casa'.",
        "aceite": [
            "Regra *a*a*a*a*a*a*a*a*c contra nome de 255 'a' + 'b' retorna em menos de 50 ms.",
            "Teste unitário cobre o caso patológico.",
            "Comportamento dos padrões existentes (*.pdf; invoice?) permanece igual (testes atuais passam).",
        ],
    },
    {
        "titulo": "[Segurança] Pré-visualização lê linha inteira de arquivos grandes na thread de interface",
        "labels": "security, severidade:baixa",
        "descricao": "Preview() usa File.ReadLines(path).Take(6) e só depois corta cada linha para 80 caracteres (FenceWindow.cs:377-378), chamado ao abrir o tooltip (FenceWindow.cs:342).",
        "evidencia": "src/GWindows.App/FenceWindow.cs:378\n  string.Join(\"\\n\", File.ReadLines(path).Take(6).Select(l => l.Length > 80 ? l[..80] : l))\nTeste: arquivo de 300 MB em linha única -> 1055 ms e pico de 2251 MB de working set.",
        "impacto": "Travamento e pico de memória de gigabytes ao passar o mouse sobre um arquivo grande de linha única (p.ex. na cerca 'Maiores Arquivos').",
        "correcao": "Ler no máximo ~4 KB com StreamReader, ignorar arquivos acima de um limite e executar fora da thread de interface.",
        "aceite": [
            "Passar o mouse sobre um .txt de 300 MB sem quebras de linha não aumenta a memória em mais de alguns MB.",
            "A interface não bloqueia durante a pré-visualização.",
            "Teste automatizado do leitor limitado cobre linha única gigante.",
        ],
    },
    {
        "titulo": "[Segurança] Notas em claro replicadas em snapshots e exportações",
        "labels": "security, severidade:informativa",
        "descricao": "O texto do widget Notas é gravado em layout.json (FenceWindow.cs:296-297; Layout.cs:24) e copiado para snapshots (App.xaml.cs:191) e exportações (App.xaml.cs:218).",
        "evidencia": "src/GWindows.App/FenceWindow.cs:297\n  box.TextChanged += (_, _) => { _d.Notes = box.Text; App.SaveSoon(); };\nsrc/GWindows.App/App.xaml.cs:191\n  File.Copy(LayoutPath, Path.Combine(SnapDir, ...), true);",
        "impacto": "Cópias de dados potencialmente sensíveis em disco por até 30 dias e em arquivos exportados.",
        "correcao": "Avisar o usuário no widget, excluir Notes de snapshots/exportação por padrão ou cifrar com DPAPI (escopo do usuário).",
        "aceite": [
            "O widget informa que o conteúdo não é cifrado.",
            "Snapshots e exportações não contêm o texto das notas (ou o contêm cifrado).",
            "Teste verifica que a exportação padrão não inclui Notes.",
        ],
    },
    {
        "titulo": "[Segurança] Endurecer execução local: explorer.exe sem caminho completo, mutex previsível e binário não assinado",
        "labels": "security, severidade:informativa",
        "descricao": "Três pontos de baixo risco (mesmo usuário, sem elevação): FenceWindow.cs:180 inicia 'explorer.exe' sem caminho completo e com argumento concatenado; App.xaml.cs:89-90 usa mutex de nome previsível que permite impedir a abertura; App.xaml.cs:15-24 registra no Run um executável não assinado em pasta gravável pelo usuário.",
        "evidencia": "src/GWindows.App/FenceWindow.cs:180\n  Process.Start(\"explorer.exe\", $\"/select,\\\"{path}\\\"\")\nsrc/GWindows.App/App.xaml.cs:89-90\n  _single = new Mutex(true, \"GWindows.SingleInstance\", out var first); if (!first) { Shutdown(); return; }\nsrc/GWindows.App/App.xaml.cs:23\n  k.SetValue(\"G-Windows\", $\"\\\"{Environment.ProcessPath}\\\"\");",
        "impacto": "Execução de binário plantado ou bloqueio silencioso do aplicativo por código já em execução como o mesmo usuário.",
        "correcao": "Usar caminho completo do Explorer e ArgumentList; nome de mutex com identificador do usuário e aviso ao detectar instância; assinar o executável.",
        "aceite": [
            "'Mostrar no Explorer' usa caminho absoluto do Windows e ArgumentList.",
            "Segunda instância avisa/ativa a primeira em vez de encerrar sem mensagem.",
            "O executável publicado é assinado (ou a decisão de não assinar está documentada).",
        ],
    },
]
