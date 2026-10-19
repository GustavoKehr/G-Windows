using System.Globalization;

namespace GWindows;

/// <summary>UI language. Source strings are Portuguese; L.T returns the English text when English is selected.
/// User data (fence names, profiles) is never translated.</summary>
static class L
{
    public static string Current = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? "pt" : "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Current == "pt" ? "pt-BR" : "en-US");

    public static string T(string pt) => Current == "en" && En.TryGetValue(pt, out var e) ? e : pt;

    static readonly Dictionary<string, string> En = new()
    {
        // tray
        ["Nova cerca"] = "New fence",
        ["Alinhar todas as cercas à grade"] = "Align all fences to grid",
        ["Perfis"] = "Profiles",
        ["Mostrar cercas"] = "Show fences",
        ["Ocultar ícones do desktop"] = "Hide desktop icons",
        ["Duplo clique no desktop mostra/oculta"] = "Double-click desktop toggles fences",
        ["Iniciar com o Windows"] = "Start with Windows",
        ["Sair"] = "Quit",
        ["Idioma"] = "Language",
        ["Restaurar snapshot"] = "Restore snapshot",
        ["Exportar layout..."] = "Export layout...",
        ["Importar layout..."] = "Import layout...",
        ["Salvar perfil atual como..."] = "Save current profile as...",
        ["Nome do perfil"] = "Profile name",
        ["O G-Windows já está em execução. Procure o ícone na bandeja do Windows."] = "G-Windows is already running. Look for its icon in the Windows tray.",
        ["Este layout contém {0} caminho(s) de rede (\\\\servidor\\...). Ao carregar, o Windows se conecta a esses servidores. Continuar?"] = "This layout contains {0} network path(s) (\\\\server\\...). When it loads, Windows connects to those servers. Continue?",
        // palette
        ["> Nova cerca"] = "> New fence",
        ["> Alternar cercas"] = "> Toggle fences",
        ["> Perfil: "] = "> Profile: ",
        // item menu
        ["Abrir"] = "Open",
        ["Mostrar no Explorer"] = "Show in Explorer",
        ["Propriedades"] = "Properties",
        ["Mais opções do Windows..."] = "More Windows options...",
        ["Remover da cerca"] = "Remove from fence",
        // fence menu
        ["Desenrolar"] = "Unroll",
        ["Enrolar (recolher na barra de título)"] = "Roll up (collapse to title bar)",
        ["Ordenar por nome"] = "Sort by name",
        ["Ordenar por tipo"] = "Sort by type",
        ["Ordenar por data"] = "Sort by date",
        ["Ordenar por tamanho"] = "Sort by size",
        ["Regra de captura automática (ex.: *.pdf; *.docx)..."] = "Auto-capture rule (e.g. *.pdf; *.docx)...",
        ["Regra desta cerca (vazio = sem regra)"] = "Rule for this fence (empty = no rule)",
        ["Cor: "] = "Color: ",
        ["Escuro"] = "Dark", ["Azul"] = "Blue", ["Verde"] = "Green", ["Roxo"] = "Purple", ["Vermelho"] = "Red",
        ["Colar caminho(s) da área de transferência (Ctrl+V)"] = "Paste path(s) from clipboard (Ctrl+V)",
        ["Tipo: manual"] = "Type: manual",
        ["Tipo: portal de pasta..."] = "Type: folder portal...",
        ["Caminho da pasta"] = "Folder path",
        ["Tipo: inteligente - recentes"] = "Type: smart - recent files",
        ["Tipo: inteligente - pastas mais acessadas"] = "Type: smart - most used folders",
        ["Tipo: inteligente - arquivos grandes"] = "Type: smart - large files",
        ["Tipo: inteligente - não tocados há 90 dias"] = "Type: smart - untouched for 90 days",
        ["Tipo: widget relógio"] = "Type: clock widget",
        ["Tipo: widget notas"] = "Type: notes widget",
        ["Alinhar à grade"] = "Align to grid",
        ["Arquivos Recentes"] = "Recent Files",
        ["Maiores Arquivos"] = "Largest Files",
        ["Não Tocados (90 dias)"] = "Untouched (90 days)",
        ["Pastas Mais Acessadas"] = "Most Used Folders",
        ["Relógio"] = "Clock",
        ["Notas"] = "Notes",
        ["Mobilidade livre (soltar fora da grade)"] = "Free movement (drop anywhere)",
        ["Excluir cerca (arquivos não são tocados)"] = "Delete fence (files are not touched)",
        ["Arraste pastas ou arquivos para cá,\nou cole o caminho (Ctrl+V)."] = "Drag folders or files here,\nor paste a path (Ctrl+V).",
    };
}