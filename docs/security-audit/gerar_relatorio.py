# -*- coding: utf-8 -*-
"""Gera docs/security-audit/relatorio-auditoria-seguranca.pdf a partir de achados.py.

Uso (dentro do venv local):
    docs/security-audit/.venv/Scripts/python.exe docs/security-audit/gerar_relatorio.py
Dependências (instaladas só no venv): reportlab, matplotlib (e pymupdf apenas para rasterizar/verificar).
"""
import datetime
import os
import sys
from xml.sax.saxutils import escape

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import cm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, CondPageBreak, Frame, Image, KeepTogether, NextPageTemplate,
                                PageBreak, PageTemplate, Paragraph, Spacer, Table, TableStyle)

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import achados as A  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "relatorio-auditoria-seguranca.pdf")
BUILD = os.path.join(HERE, "build")
os.makedirs(BUILD, exist_ok=True)

TITULO = f"Relatório de Auditoria de Segurança — {A.PROJETO}"
HOJE = datetime.date.today()
MESES = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"]
DATA_EXTENSO = f"{HOJE.day} de {MESES[HOJE.month - 1]} de {HOJE.year}"

# ---------- fontes ----------
FD = r"C:\Windows\Fonts"
for name, f in [("Arial", "arial.ttf"), ("Arial-Bold", "arialbd.ttf"), ("Arial-Italic", "ariali.ttf"), ("Arial-BoldItalic", "arialbi.ttf"), ("Mono", "consola.ttf"), ("Mono-Bold", "consolab.ttf")]:
    pdfmetrics.registerFont(TTFont(name, os.path.join(FD, f)))
pdfmetrics.registerFontFamily("Arial", normal="Arial", bold="Arial-Bold", italic="Arial-Italic", boldItalic="Arial-BoldItalic")
pdfmetrics.registerFontFamily("Mono", normal="Mono", bold="Mono-Bold", italic="Mono", boldItalic="Mono-Bold")

NAVY = colors.HexColor("#0F172A")
GRAY = colors.HexColor("#6B7280")
LIGHT = colors.HexColor("#F3F4F6")
LINE = colors.HexColor("#D1D5DB")

ss = {
    "body": ParagraphStyle("body", fontName="Arial", fontSize=9.2, leading=13, textColor=colors.HexColor("#111827")),
    "small": ParagraphStyle("small", fontName="Arial", fontSize=8, leading=10.8, textColor=colors.HexColor("#111827")),
    "tiny": ParagraphStyle("tiny", fontName="Arial", fontSize=7.4, leading=9.6, textColor=GRAY),
    "h1": ParagraphStyle("h1", fontName="Arial-Bold", fontSize=17, leading=21, textColor=NAVY, spaceBefore=4, spaceAfter=8),
    "h2": ParagraphStyle("h2", fontName="Arial-Bold", fontSize=12, leading=15, textColor=NAVY, spaceBefore=10, spaceAfter=5),
    "h3": ParagraphStyle("h3", fontName="Arial-Bold", fontSize=10, leading=13, textColor=NAVY, spaceBefore=7, spaceAfter=3),
    "cell": ParagraphStyle("cell", fontName="Arial", fontSize=8, leading=10.4),
    "cellb": ParagraphStyle("cellb", fontName="Arial-Bold", fontSize=8, leading=10.4),
    "hdr": ParagraphStyle("hdr", fontName="Arial-Bold", fontSize=8, leading=10.4, textColor=colors.white),
    "chip": ParagraphStyle("chip", fontName="Arial-Bold", fontSize=7.4, leading=9.2, textColor=colors.white, alignment=TA_CENTER),
    "code": ParagraphStyle("code", fontName="Mono", fontSize=7.4, leading=9.4, backColor=LIGHT, borderPadding=(2, 3, 2, 3), spaceAfter=1),
    "issue": ParagraphStyle("issue", fontName="Mono", fontSize=7.4, leading=9.8, backColor=LIGHT),
    "codesmall": ParagraphStyle("codesmall", fontName="Mono", fontSize=7, leading=8.8),
    "cover_title": ParagraphStyle("ct", fontName="Arial-Bold", fontSize=25, leading=31, textColor=colors.white),
    "cover_sub": ParagraphStyle("cs", fontName="Arial", fontSize=11.5, leading=15, textColor=colors.HexColor("#CBD5E1")),
}


def P(text, style="body", raw=False):
    return Paragraph(text if raw else escape(text), ss[style])


def pre(text):
    """Texto monoespaçado preservando quebras e recuos."""
    out = []
    for line in text.split("\n"):
        stripped = line.lstrip(" ")
        lead = "&nbsp;" * (len(line) - len(stripped))
        out.append(lead + escape(stripped))
    return "<br/>".join(out)


def chip(sev):
    t = Table([[Paragraph(sev.upper(), ss["chip"])]], colWidths=[2.0 * cm], rowHeights=[0.5 * cm])
    t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), colors.HexColor(A.SEV_COR.get(sev, A.COR_FORTE))),
                           ("ROUNDEDCORNERS", [3, 3, 3, 3]), ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                           ("LEFTPADDING", (0, 0), (-1, -1), 1), ("RIGHTPADDING", (0, 0), (-1, -1), 1),
                           ("TOPPADDING", (0, 0), (-1, -1), 1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1)]))
    return t


def chip_cor(texto, cor):
    t = Table([[Paragraph(texto.upper(), ss["chip"])]], colWidths=[2.0 * cm], rowHeights=[0.5 * cm])
    t.setStyle(TableStyle([("BACKGROUND", (0, 0), (-1, -1), colors.HexColor(cor)), ("ROUNDEDCORNERS", [3, 3, 3, 3]),
                           ("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("LEFTPADDING", (0, 0), (-1, -1), 1), ("RIGHTPADDING", (0, 0), (-1, -1), 1),
                           ("TOPPADDING", (0, 0), (-1, -1), 1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1)]))
    return t


def tabela(dados, larguras, header=True, zebra=True, extra=None):
    t = Table(dados, colWidths=larguras, repeatRows=1 if header else 0)
    est = [("VALIGN", (0, 0), (-1, -1), "TOP"), ("GRID", (0, 0), (-1, -1), 0.4, LINE),
           ("LEFTPADDING", (0, 0), (-1, -1), 4), ("RIGHTPADDING", (0, 0), (-1, -1), 4),
           ("TOPPADDING", (0, 0), (-1, -1), 3), ("BOTTOMPADDING", (0, 0), (-1, -1), 3)]
    if header:
        est += [("BACKGROUND", (0, 0), (-1, 0), NAVY)]
    if zebra:
        for i in range(1 if header else 0, len(dados)):
            if i % 2 == 0:
                est.append(("BACKGROUND", (0, i), (-1, i), colors.HexColor("#F9FAFB")))
    if extra:
        est += extra
    t.setStyle(TableStyle(est))
    return t


# ---------- dados derivados ----------
def contar_linhas(rel):
    p = os.path.join(ROOT, rel)
    with open(p, encoding="utf-8", errors="replace") as f:
        return sum(1 for _ in f)


def arquivos_escopo():
    itens = []
    for base in ("src", "tests"):
        for d, ds, fs in os.walk(os.path.join(ROOT, base)):
            ds[:] = [x for x in ds if x not in ("bin", "obj")]
            for f in sorted(fs):
                if f.endswith((".cs", ".xaml", ".csproj", ".manifest")):
                    itens.append(os.path.relpath(os.path.join(d, f), ROOT).replace("\\", "/"))
    for f in ("nuget.config",):
        if os.path.exists(os.path.join(ROOT, f)):
            itens.append(f)
    return sorted(itens)


ESCOPO = arquivos_escopo()
LINHAS = {f: contar_linhas(f) for f in ESCOPO}
TOTAL_LINHAS = sum(LINHAS.values())

cont_sev = {s: sum(1 for a in A.ACHADOS if a["sev"] == s) for s in A.SEV_ORDEM}
TOTAL = len(A.ACHADOS)


def cat_nome(c):
    return A.OUTROS if c == "outros" else f"{c}. {A.CATEGORIAS[c - 1]}"


# ---------- gráficos ----------
def grafico_rosca():
    vals = [(s, n) for s, n in cont_sev.items() if n > 0]
    fig, ax = plt.subplots(figsize=(3.6, 3.3), dpi=220)
    wedges, _ = ax.pie([n for _, n in vals], colors=[A.SEV_COR[s] for s, _ in vals], startangle=90, counterclock=False,
                       wedgeprops=dict(width=0.38, edgecolor="white", linewidth=2))
    ax.text(0, 0.07, str(TOTAL), ha="center", va="center", fontsize=26, fontweight="bold", color="#0F172A")
    ax.text(0, -0.2, "achados", ha="center", va="center", fontsize=9, color="#6B7280")
    ax.legend(wedges, [f"{s} ({n})" for s, n in vals], loc="upper center", bbox_to_anchor=(0.5, 0.02), ncol=2, frameon=False, fontsize=8)
    ax.set_title("Achados por severidade", fontsize=10, fontweight="bold", color="#0F172A")
    fig.tight_layout()
    p = os.path.join(BUILD, "rosca.png")
    fig.savefig(p, transparent=False, facecolor="white")
    plt.close(fig)
    return p


def grafico_barras():
    cats = list(range(1, 23)) + ["outros"]
    achados_por = {c: {s: 0 for s in A.SEV_ORDEM} for c in cats}
    for a in A.ACHADOS:
        achados_por[a["cat"]][a["sev"]] += 1
    fortes_por = {c: 0 for c in cats}
    for c, *_ in A.PONTOS_FORTES:
        fortes_por[c] += 1
    fig, ax = plt.subplots(figsize=(6.9, 7.4), dpi=200)
    ys = list(range(len(cats)))[::-1]
    h = 0.36
    for y, c in zip(ys, cats):
        left = 0
        for s in A.SEV_ORDEM:
            n = achados_por[c][s]
            if n:
                ax.barh(y + h / 2, n, left=left, height=h, color=A.SEV_COR[s], edgecolor="white")
                left += n
        if fortes_por[c]:
            ax.barh(y - h / 2, fortes_por[c], height=h, color=A.COR_FORTE, edgecolor="white")
        if not achados_por[c] or (sum(achados_por[c].values()) == 0 and fortes_por[c] == 0):
            status = A.COBERTURA.get(c if c != "outros" else 0, ("", ""))[0]
            ax.text(0.04, y, "não se aplica" if status == "na" else "sem achados", va="center", fontsize=6.8, color="#9CA3AF", style="italic")
    ax.set_yticks(ys)
    ax.set_yticklabels([cat_nome(c) if c != "outros" else A.OUTROS for c in cats], fontsize=7.2)
    ax.set_xlim(0, 3.2)
    ax.set_xticks([0, 1, 2, 3])
    ax.tick_params(axis="x", labelsize=7)
    ax.grid(axis="x", color="#E5E7EB", linewidth=0.6)
    ax.set_axisbelow(True)
    for sp in ("top", "right"):
        ax.spines[sp].set_visible(False)
    handles = [plt.Rectangle((0, 0), 1, 1, color=A.SEV_COR[s]) for s in A.SEV_ORDEM if cont_sev[s]] + [plt.Rectangle((0, 0), 1, 1, color=A.COR_FORTE)]
    rotulos = [s for s in A.SEV_ORDEM if cont_sev[s]] + ["Ponto forte (verificado)"]
    ax.legend(handles, rotulos, loc="upper center", bbox_to_anchor=(0.45, -0.04), ncol=len(rotulos), fontsize=7.4, frameon=False)
    ax.set_title("Achados e pontos fortes por categoria", fontsize=10, fontweight="bold", color="#0F172A")
    fig.tight_layout()
    p = os.path.join(BUILD, "barras.png")
    fig.savefig(p, facecolor="white")
    plt.close(fig)
    return p


# ---------- página: cabeçalho/rodapé ----------
def pagina_capa(canv, doc):
    canv.saveState()
    w, h = A4
    canv.setFillColor(NAVY)
    canv.rect(0, h - 11.5 * cm, w, 11.5 * cm, stroke=0, fill=1)
    canv.setFillColor(colors.HexColor("#D97706"))
    canv.rect(0, h - 11.5 * cm, w, 0.22 * cm, stroke=0, fill=1)
    canv.restoreState()


def pagina_normal(canv, doc):
    canv.saveState()
    w, h = A4
    canv.setStrokeColor(LINE)
    canv.setLineWidth(0.5)
    canv.line(2 * cm, h - 1.45 * cm, w - 2 * cm, h - 1.45 * cm)
    canv.line(2 * cm, 1.55 * cm, w - 2 * cm, 1.55 * cm)
    canv.setFont("Arial-Bold", 7.6)
    canv.setFillColor(NAVY)
    canv.drawString(2 * cm, h - 1.2 * cm, TITULO)
    canv.setFont("Arial", 7.6)
    canv.setFillColor(GRAY)
    canv.drawRightString(w - 2 * cm, h - 1.2 * cm, f"Auditoria de {DATA_EXTENSO}")
    canv.drawString(2 * cm, 1.05 * cm, TITULO)
    canv.drawRightString(w - 2 * cm, 1.05 * cm, f"Página {doc.page}")
    canv.restoreState()


class Doc(BaseDocTemplate):
    pass


def montar():
    doc = Doc(OUT, pagesize=A4, leftMargin=2 * cm, rightMargin=2 * cm, topMargin=2 * cm, bottomMargin=2 * cm,
              title=TITULO, author="Auditoria de segurança", subject="Checklist de Segurança (Auditoria de Aplicação)")
    larg, alt = A4
    frame_capa = Frame(2 * cm, 2 * cm, larg - 4 * cm, alt - 4 * cm, id="capa", leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    frame = Frame(2 * cm, 2 * cm, larg - 4 * cm, alt - 4 * cm - 0.3 * cm, id="main", leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    doc.addPageTemplates([PageTemplate("capa", frames=[frame_capa], onPage=pagina_capa),
                          PageTemplate("normal", frames=[frame], onPage=pagina_normal)])
    return doc


W = A4[0] - 4 * cm  # largura útil (17 cm)


def secao_capa():
    s = []
    s.append(Spacer(1, 2.2 * cm))
    s.append(P("AUDITORIA DE SEGURANÇA DE APLICAÇÃO", "cover_sub"))
    s.append(Spacer(1, 0.3 * cm))
    s.append(P(TITULO, "cover_title"))
    s.append(Spacer(1, 0.5 * cm))
    s.append(P(f"Data: {DATA_EXTENSO}", "cover_sub"))
    s.append(Spacer(1, 6.2 * cm))
    s.append(P("Escopo auditado", "h2"))
    n_cs = sum(1 for f in ESCOPO if f.endswith(".cs"))
    s.append(P(f"Todo o código-fonte do repositório: GWindows.Core (biblioteca de modelo/regras) e GWindows.App (aplicativo WPF), mais os testes, arquivos de projeto, "
               f"manifesto e nuget.config — {len(ESCOPO)} arquivos, {TOTAL_LINHAS} linhas, {n_cs} deles em C#. Binários gerados (bin/obj) e o ambiente virtual desta auditoria ficaram fora do escopo.", "body"))
    s.append(Spacer(1, 0.2 * cm))
    s.append(P("Nota metodológica", "h2"))
    s.append(P("A stack detectada é um <b>aplicativo desktop Windows local</b> (C#, .NET 8, WPF/WinForms), sem servidor, sem banco de dados, sem autenticação e sem requisições de rede. "
               "Por isso a checklist de 22 categorias, pensada para aplicações web, foi mapeada assim: categorias de servidor/web (SQL Injection, XSS, CSRF, CORS, cookies, tokens, rate limit, "
               "autenticação, IDOR, enumeração, rotas administrativas, prompt injection, senhas, força bruta) foram marcadas <b>não se aplica</b>, com a justificativa verificada no código. "
               "As categorias com equivalente local foram avaliadas por analogia: o <i>layout.json</i> e o arquivo de importação equivalem a entrada não confiável (validação, upload); "
               "caminhos UNC em itens de cerca equivalem a SSRF local (o sistema contacta um host controlado por terceiros); leitura de arquivos grandes e regex equivalem a abuso de recursos (DoS).", "body", raw=True))
    s.append(Spacer(1, 0.2 * cm))
    s.append(P("<b>Como foi verificado:</b> leitura integral de todos os arquivos com numeração de linhas; buscas por padrões (segredos, rede, execução de processos, desserialização); "
               "<i>dotnet list package --vulnerable --include-transitive</i>; e programas de comprovação executados num projeto isolado fora do repositório, usando o carregador e as regras reais "
               "(nulos aceitos, geometria extrema, tempo do regex, consumo de memória). <b>Limites:</b> o comportamento SMB/NTLM do Windows e a partida completa do aplicativo com um layout hostil "
               "não foram exercitados; os achados A-01, A-02 e A-06 indicam o que foi confirmado e o que decorre da leitura do código e do comportamento documentado do sistema.", "small", raw=True))
    s.append(NextPageTemplate("normal"))
    s.append(PageBreak())
    return s


def secao_resumo(img_rosca, img_barras):
    s = [P("Resumo executivo", "h1")]
    crit = cont_sev["Crítica"] + cont_sev["Alta"]
    veredito = ("Não foram encontradas falhas críticas nem altas. " if crit == 0 else "") + \
        f"Foram verificados <b>{TOTAL} achados</b>: {cont_sev['Média']} de severidade média, {cont_sev['Baixa']} baixas e {cont_sev['Informativa']} informativos. " \
        "O risco central é tratar o arquivo de layout (importado, restaurado ou adulterado em disco) como confiável: ele define quais caminhos o aplicativo acessa, " \
        "enumera, observa e abre com um clique, e não passa por nenhuma validação. As demais falhas são negações de serviço locais de baixo impacto. " \
        f"Em contrapartida, {len(A.PONTOS_FORTES)} pontos fortes foram comprovados, entre eles a ausência de rede, de segredos e de elevação de privilégio. " \
        "<b>Depois da auditoria, os achados A-02, A-03, A-04 e A-06 foram corrigidos, A-01 e A-07 foram mitigados e A-05 e A-08 foram aceitos</b> (status por achado na seção de detalhes; os trechos e linhas citados referem-se ao código no momento da auditoria)."
    s.append(P(veredito, "body", raw=True))
    s.append(Spacer(1, 0.3 * cm))
    # tabela por severidade
    linhas = [[P("Severidade", "hdr"), P("Quantidade", "hdr"), P("Achados", "hdr")]]
    for sev in A.SEV_ORDEM:
        ids = ", ".join(a["id"] for a in A.ACHADOS if a["sev"] == sev) or "—"
        linhas.append([chip(sev), P(str(cont_sev[sev]), "cellb"), P(ids, "cell")])
    linhas.append([chip_cor("Ponto forte", A.COR_FORTE), P(str(len(A.PONTOS_FORTES)), "cellb"), P("verificados com evidência (ver seção seguinte)", "cell")])
    s.append(tabela(linhas, [3.0 * cm, 2.6 * cm, W - 5.6 * cm]))
    s.append(Spacer(1, 0.4 * cm))
    s.append(Image(img_rosca, width=8.2 * cm, height=7.5 * cm, hAlign="CENTER"))
    s.append(PageBreak())
    s.append(P("Achados e pontos fortes por categoria", "h2"))
    s.append(Image(img_barras, width=W, height=W * 7.4 / 6.9, hAlign="CENTER"))
    s.append(P("Cada categoria é contada uma vez pela sua categoria principal (o achado A-01 aparece em 12; o cruzamento com SSRF está descrito no texto). "
               "'Não se aplica' indica categoria sem equivalente na stack.", "tiny"))
    s.append(PageBreak())
    return s


def secao_fortes_fracos():
    s = [P("Pontos fortes e pontos fracos", "h1"), P("Pontos fortes (verificados)", "h2")]
    linhas = [[P("Categoria", "hdr"), P("O que está protegido", "hdr"), P("Evidência", "hdr")]]
    for c, titulo, detalhe, onde in A.PONTOS_FORTES:
        linhas.append([P(cat_nome(c), "cell"), P(f"<b>{escape(titulo)}.</b> {escape(detalhe)}", "cell", raw=True), P(onde, "cell")])
    s.append(tabela(linhas, [3.4 * cm, 9.0 * cm, W - 12.4 * cm], extra=[("LINEBEFORE", (0, 1), (0, -1), 3, colors.HexColor(A.COR_FORTE))]))
    s.append(P("Pontos fracos (riscos centrais)", "h2"))
    fracos = [
        ("Layout tratado como confiável (A-01, A-02)", "Entrada externa (importação, snapshot adulterado, layout.json editado) é aceita sem validar caminhos, nulos ou faixas: risco de acesso a recursos de rede, execução com um clique e queda na abertura."),
        ("Operações sem limites de recurso (A-03, A-04)", "Regex sem timeout e leitura de linha inteira executadas na thread de interface permitem congelar o aplicativo ou consumir gigabytes de memória."),
        ("Dados em claro e endurecimento local (A-05 a A-08)", "Notas copiadas em claro para snapshots/exportações; execução do Explorer sem caminho completo; mutex previsível; binário não assinado em pasta gravável."),
    ]
    linhas = [[P("Risco", "hdr"), P("Descrição", "hdr")]]
    for t, d in fracos:
        linhas.append([P(t, "cellb"), P(d, "cell")])
    s.append(tabela(linhas, [5.2 * cm, W - 5.2 * cm]))
    s.append(PageBreak())
    return s


def secao_cobertura():
    s = [P("Cobertura da checklist (22 categorias)", "h1")]
    s.append(P("Resultado por categoria: <b>Achado</b> (há falha verificada), <b>Parcial</b> (parte protegida, parte com achado), <b>Correto</b> (verificado e protegido) ou <b>N/A</b> (sem equivalente na stack, com justificativa).", "small", raw=True))
    s.append(Spacer(1, 0.2 * cm))
    linhas = [[P("#", "hdr"), P("Categoria", "hdr"), P("Resultado", "hdr"), P("Justificativa / evidência", "hdr")]]
    for i, nome in enumerate(A.CATEGORIAS, 1):
        st, txt = A.COBERTURA[i]
        if st == "achado":
            sev = [a["sev"] for a in A.ACHADOS if a["cat"] == i or i in a.get("extra_cat", [])]
            pior = min(sev, key=A.SEV_ORDEM.index) if sev else "Informativa"
            c = chip(pior)
        elif st == "correto":
            c = chip_cor("Correto", A.COR_FORTE)
        elif st == "parcial":
            c = chip_cor("Parcial", "#D97706")
        else:
            c = chip_cor("N/A", "#9CA3AF")
        linhas.append([P(str(i), "cell"), P(nome, "cellb"), c, P(txt, "cell")])
    s.append(tabela(linhas, [0.8 * cm, 3.8 * cm, 2.3 * cm, W - 6.9 * cm]))
    s.append(PageBreak())
    return s


def bloco_achado(a):
    cor = A.SEV_COR[a["sev"]]
    topo = Table([[chip(a["sev"]), P(f"<b>{a['id']}</b> — {escape(a['titulo'])}", "body", raw=True)]], colWidths=[2.3 * cm, W - 2.3 * cm])
    topo.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "MIDDLE"), ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F9FAFB")),
                              ("LINEBEFORE", (0, 0), (0, -1), 3, colors.HexColor(cor)), ("LEFTPADDING", (0, 0), (-1, -1), 6),
                              ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 4)]))
    out = [topo, Spacer(1, 0.12 * cm)]
    st, txt = a["status"]
    out.append(P(f"<b>Status após a revisão de {DATA_EXTENSO}: {escape(st)}.</b> {escape(txt)}", "small", raw=True))
    out.append(P("Onde (arquivo:linha) e trecho — estado auditado", "h3"))
    for arq, linhas, trecho in a["locais"]:
        out.append(P(f"<b>{escape(arq)}:{escape(linhas)}</b>", "small", raw=True))
        out.append(Paragraph(pre(trecho), ss["code"]))
        out.append(Spacer(1, 0.08 * cm))
    for titulo, chave in [("Por que é explorável", "porque"), ("Condições de explorabilidade e verificação", "condicoes"), ("Impacto", "impacto"), ("Correção sugerida", "correcao")]:
        out.append(KeepTogether([P(titulo, "h3"), P(a[chave], "body")]))
    out.append(Spacer(1, 0.5 * cm))
    return out


def secao_achados():
    s = [P("Achados detalhados por categoria", "h1")]
    cats = sorted({a["cat"] for a in A.ACHADOS}, key=lambda c: 99 if c == "outros" else c)
    for c in cats:
        s.append(CondPageBreak(5 * cm))
        s.append(P(cat_nome(c), "h2"))
        itens = [a for a in A.ACHADOS if a["cat"] == c]
        linhas = [[P("Severidade", "hdr"), P("Arquivo:linha", "hdr"), P("Descrição", "hdr")]]
        for a in itens:
            locais = "<br/>".join(f"{escape(arq.split('/')[-1])}:{escape(l)}" for arq, l, _ in a["locais"])
            cross = ""
            if a.get("extra_cat"):
                cross = " <i>(relacionado a: " + ", ".join(cat_nome(x) for x in a["extra_cat"]) + ")</i>"
            linhas.append([chip(a["sev"]), P(locais, "cell", raw=True), P(f"<b>{a['id']}</b> — {escape(a['titulo'])}{cross}<br/><i>Status: {escape(a['status'][0])}</i>", "cell", raw=True)])
        s.append(tabela(linhas, [2.4 * cm, 4.3 * cm, W - 6.7 * cm]))
        s.append(Spacer(1, 0.25 * cm))
        for a in itens:
            s.extend(bloco_achado(a))
    s.append(PageBreak())
    s.append(P("Achados por arquivo (linha a linha)", "h1"))
    por_arq = {}
    for a in A.ACHADOS:
        for arq, l, _ in a["locais"]:
            por_arq.setdefault(arq, []).append((l, a["id"], a["sev"]))
    linhas = [[P("Arquivo", "hdr"), P("Linha(s)", "hdr"), P("Achado", "hdr"), P("Severidade", "hdr")]]
    for arq in sorted(por_arq):
        for l, i, sev in sorted(por_arq[arq], key=lambda x: int(x[0].replace(" ", "").split(",")[0].split("-")[0])):
            linhas.append([P(arq, "cell"), P(l, "cell"), P(i, "cellb"), chip(sev)])
    s.append(tabela(linhas, [6.6 * cm, 3.0 * cm, 2.2 * cm, W - 11.8 * cm]))
    s.append(Spacer(1, 0.3 * cm))
    s.append(P("Arquivos auditados sem achados (todas as linhas lidas): " + ", ".join(sorted(f for f in ESCOPO if f.endswith(".cs") and f not in por_arq)) + ".", "tiny"))
    s.append(PageBreak())
    return s


def secao_recomendacoes():
    s = [P("Recomendações priorizadas", "h1")]
    linhas = [[P("Prioridade", "hdr"), P("Ação", "hdr"), P("Detalhe", "hdr")]]
    cores = {"P1": "#B91C1C", "P2": "#D97706", "P3": "#2563EB", "P4": "#6B7280"}
    for pr, titulo, det in A.RECOMENDACOES:
        linhas.append([chip_cor(pr, cores[pr]), P(titulo, "cellb"), P(det, "cell")])
    s.append(tabela(linhas, [2.4 * cm, 5.4 * cm, W - 7.8 * cm]))
    s.append(Spacer(1, 0.3 * cm))
    s.append(P("P1 = corrigir primeiro; P2 = próxima iteração; P3 = endurecimento; P4 = rotina.", "tiny"))
    s.append(PageBreak())
    return s


def issue_md(n, i):
    linhas = [f"# {i['titulo']}", "", f"**Labels sugeridas:** {', '.join('`' + x.strip() + '`' for x in i['labels'].split(','))}", "",
              "## Descrição do problema", i["descricao"], "", "## Evidência", "```", i["evidencia"], "```", "",
              "## Impacto", i["impacto"], "", "## Sugestão de correção", i["correcao"], "", "## Critérios de aceite"]
    linhas += [f"- [ ] {c}" for c in i["aceite"]]
    return "\n".join(linhas)


def secao_issues():
    s = [P("ISSUES PARA O GITHUB", "h1")]
    s.append(P("Texto pronto para copiar e colar. As issues agrupam achados triviais relacionados (A-06, A-07 e A-08 viram uma só) para evitar excesso de issues. A-01 a A-04 e A-05 têm issue própria.", "small"))
    for n, i in enumerate(A.ISSUES, 1):
        s.append(Spacer(1, 0.35 * cm))
        s.append(Paragraph(f"<b>--- ISSUE {n} ---</b>", ParagraphStyle("d", parent=ss["issue"], backColor=colors.HexColor("#E5E7EB"), fontName="Mono-Bold", fontSize=8.2, leading=10.5)))
        md = issue_md(n, i)
        bloco = []
        for linha in md.split("\n"):
            bloco.append(Paragraph(pre(linha) if linha else "&nbsp;", ss["issue"]))
        s.extend(bloco)
        s.append(Paragraph(f"<b>--- FIM ISSUE {n} ---</b>", ParagraphStyle("d2", parent=ss["issue"], backColor=colors.HexColor("#E5E7EB"), fontName="Mono-Bold", fontSize=8.2, leading=10.5)))
        s.append(CondPageBreak(4 * cm))
    return s


def main():
    img_rosca = grafico_rosca()
    img_barras = grafico_barras()
    doc = montar()
    flow = []
    flow += secao_capa()
    flow += secao_resumo(img_rosca, img_barras)
    flow += secao_fortes_fracos()
    flow += secao_cobertura()
    flow += secao_achados()
    flow += secao_recomendacoes()
    flow += secao_issues()
    doc.build(flow)
    print("PDF gerado:", OUT)


if __name__ == "__main__":
    main()
