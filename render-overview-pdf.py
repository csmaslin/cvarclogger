#!/usr/bin/env python3
"""Renders "Program Overview and Data Flow.md" to a PDF for publish.ps1 to bundle, in place of
shipping the raw .md file. The one part plain Markdown->HTML->PDF can't handle is the Mermaid
flowchart -- markdown converts that code fence to inert text, not a diagram -- so this pre-renders
just that fenced block to a PNG with mermaid-cli (`mmdc`, via a bundled Chromium/Puppeteer) and
splices it back in as a normal image before the HTML/PDF conversion. See the mermaid_cli_setup
memory for how mmdc's Chromium was installed on this machine, and the
project_word_com_automation_environment memory for why the HTML->PDF leg reuses the same
LibreOffice-headless approach as the User Manual export rather than Word/another dependency.

Usage:
    python render-overview-pdf.py --md "docs\\Program Overview and Data Flow.md" --out publish\\CvarcLogger\\"Program Overview and Data Flow.pdf"
"""
import argparse
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import markdown
from PIL import Image

SOFFICE_EXE = r"C:\Program Files\LibreOffice\program\soffice.exe"

# Target pixel width for the embedded diagram. LibreOffice's HTML->PDF conversion doesn't reliably
# honor CSS/HTML width on an <img> -- it was embedding the diagram near its native size regardless
# of any width styling, overflowing the page -- so the PNG itself is resized to this width below
# before embedding, rather than asking LibreOffice to scale it down at conversion time.
DIAGRAM_WIDTH_PX = 650

MERMAID_BLOCK_RE = re.compile(r"```mermaid\n(.*?)```\n?", re.DOTALL)

CSS = """
body { font-family: Calibri, "Segoe UI", Arial, sans-serif; font-size: 11pt; color: #222;
       max-width: 900px; margin: 0 auto; line-height: 1.4; }
h1 { font-size: 20pt; border-bottom: 2px solid #2E5395; padding-bottom: 4px; }
h2 { font-size: 15pt; color: #2E5395; margin-top: 28px; }
h3 { font-size: 12.5pt; color: #2E5395; margin-top: 20px; }
table { border-collapse: collapse; width: 100%; margin: 10px 0; }
th, td { border: 1px solid #bbb; padding: 6px 10px; text-align: left; vertical-align: top; }
th { background: #f0f0f0; }
code { background: #f4f4f4; padding: 1px 4px; font-family: Consolas, monospace; font-size: 10pt; }
em { color: #555; }
img { max-width: 100%; display: block; margin: 14px auto; }
"""


def render_mermaid_to_png(mermaid_source: str, workdir: Path) -> Path:
    mmd_path = workdir / "diagram.mmd"
    png_path = workdir / "diagram.png"
    mmd_path.write_text(mermaid_source, encoding="utf-8")

    mmdc = shutil.which("mmdc") or shutil.which("mmdc.cmd")
    if mmdc is None:
        raise RuntimeError("mmdc (mermaid-cli) not found on PATH -- install with "
                            "'npm install -g @mermaid-js/mermaid-cli'")

    subprocess.run(
        [mmdc, "-i", str(mmd_path), "-o", str(png_path), "-b", "white", "-s", "3"],
        check=True, capture_output=True, text=True,
    )
    if not png_path.exists():
        raise RuntimeError("mmdc did not produce an output PNG")

    with Image.open(png_path) as img:
        target_height = round(img.height * (DIAGRAM_WIDTH_PX / img.width))
        img.resize((DIAGRAM_WIDTH_PX, target_height), Image.LANCZOS).save(png_path)

    return png_path


def build_html(md_path: Path, workdir: Path) -> Path:
    text = md_path.read_text(encoding="utf-8")

    match = MERMAID_BLOCK_RE.search(text)
    if match:
        png_path = render_mermaid_to_png(match.group(1), workdir)
        # An explicit pixel width attribute, not CSS max-width, because LibreOffice's HTML->PDF
        # conversion doesn't reliably downscale an oversized image to fit the page from CSS alone
        # -- it was clipping the (much wider than the page) rendered flowchart instead of shrinking
        # it. 600px comfortably fits a Letter page's content width after default margins.
        # Inline style, not the HTML width attribute or the generic `img { max-width: 100% }` rule in
        # CSS -- LibreOffice's HTML->PDF conversion gives the stylesheet's max-width higher precedence
        # than the plain width attribute, so with only that attribute set it rendered the image near
        # its full native size (784px) instead of shrinking it, overflowing the page. An inline style
        # wins over both.
        text = MERMAID_BLOCK_RE.sub(
            f'\n<p><img src="{png_path.name}" style="width:600px;max-width:100%;" alt="Data flow diagram"></p>\n\n',
            text, count=1,
        )

    body_html = markdown.markdown(text, extensions=["tables", "fenced_code"])
    html_path = workdir / "overview.html"
    html_path.write_text(
        f"<!doctype html><html><head><meta charset='utf-8'><style>{CSS}</style></head>"
        f"<body>{body_html}</body></html>",
        encoding="utf-8",
    )
    return html_path


def convert_html_to_pdf(html_path: Path, workdir: Path) -> Path:
    result = subprocess.run(
        [SOFFICE_EXE, "--headless", "--convert-to", "pdf", "--outdir", str(workdir), str(html_path)],
        capture_output=True, text=True, timeout=60,
    )
    pdf_path = html_path.with_suffix(".pdf")
    if result.returncode != 0 or not pdf_path.exists():
        raise RuntimeError(f"LibreOffice HTML->PDF conversion failed: {result.stdout}\n{result.stderr}")
    return pdf_path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--md", required=True, help="Path to the source .md file")
    parser.add_argument("--out", required=True, help="Destination .pdf path")
    args = parser.parse_args()

    md_path = Path(args.md)
    out_path = Path(args.out)
    if not md_path.exists():
        raise SystemExit(f"Source Markdown file not found: {md_path}")

    with tempfile.TemporaryDirectory(prefix="cvarclogger-overview-") as tmp:
        workdir = Path(tmp)
        html_path = build_html(md_path, workdir)
        pdf_path = convert_html_to_pdf(html_path, workdir)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy(pdf_path, out_path)

    print(f"Rendered: {out_path}")


if __name__ == "__main__":
    main()
