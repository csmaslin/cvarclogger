"""Regenerates docs/CvarcLogger User Manual.json from docs/CvarcLogger User Manual.docx.

Run automatically as an MSBuild target before every CvarcLogger.App build (see
CvarcLogger.App.csproj's RegenerateManualJson target), so the in-app Help window's manual
always reflects whatever is currently in the .docx -- matching how publish.ps1 already
regenerates the manual PDF from the same .docx via LibreOffice on every publish.

Each Heading 1 becomes one Help window chapter. Heading 2 becomes an upper-cased
sub-heading line within the surrounding text block. List Bullet paragraphs become
"- " lines. Tables become their own {"type": "table", "rows": [...]} block, rendered
by HelpWindow as a real WPF Table instead of flattened text.
"""

import json
import sys
from pathlib import Path

import docx
from docx.oxml.ns import qn
from docx.table import Table as DocxTable
from docx.text.paragraph import Paragraph

REPO_ROOT = Path(__file__).resolve().parent
DOCX_PATH = REPO_ROOT / "docs" / "CvarcLogger User Manual.docx"
JSON_PATH = REPO_ROOT / "docs" / "CvarcLogger User Manual.json"


def iter_block_items(parent):
    parent_elm = parent.element.body
    for child in parent_elm.iterchildren():
        if child.tag == qn("w:p"):
            yield Paragraph(child, parent)
        elif child.tag == qn("w:tbl"):
            yield DocxTable(child, parent)


def extract(docx_path: Path) -> list[dict]:
    doc = docx.Document(str(docx_path))

    chapters: list[dict] = []
    current_title: str | None = None
    current_blocks: list[dict] = []
    text_buffer_lines: list[str] = []

    def flush_text_buffer():
        nonlocal text_buffer_lines
        if text_buffer_lines:
            text = "\n".join(text_buffer_lines).strip("\n")
            while "\n\n\n" in text:
                text = text.replace("\n\n\n", "\n\n")
            if text.strip():
                current_blocks.append({"type": "text", "text": text})
        text_buffer_lines = []

    def flush_chapter():
        nonlocal current_title, current_blocks
        flush_text_buffer()
        if current_title is not None:
            chapters.append({"title": current_title, "blocks": current_blocks})
        current_blocks = []

    for block in iter_block_items(doc):
        if isinstance(block, Paragraph):
            style = block.style.name
            text = block.text.strip()
            if style == "Heading 1":
                flush_text_buffer()
                flush_chapter()
                current_title = text
            elif style == "Heading 2":
                if text:
                    text_buffer_lines.append("")
                    text_buffer_lines.append(text.upper())
            elif style == "List Bullet":
                if text:
                    text_buffer_lines.append(f"- {text}")
            else:
                text_buffer_lines.append(text if text else "")
        else:
            flush_text_buffer()
            rows = [[cell.text.strip() for cell in row.cells] for row in block.rows]
            current_blocks.append({"type": "table", "rows": rows})

    flush_chapter()

    return [c for c in chapters if c["title"] != "Table of Contents"]


def main() -> int:
    if not DOCX_PATH.exists():
        print(f"extract-manual-json: source .docx not found at {DOCX_PATH}, skipping", file=sys.stderr)
        return 0

    chapters = extract(DOCX_PATH)

    if not chapters:
        print("extract-manual-json: extraction produced 0 chapters, leaving existing JSON untouched", file=sys.stderr)
        return 1

    with open(JSON_PATH, "w", encoding="utf-8") as f:
        json.dump({"chapters": chapters}, f, indent=2, ensure_ascii=True)

    print(f"extract-manual-json: wrote {len(chapters)} chapters to {JSON_PATH}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
