"""Markdown -> HTML -> PDF via xhtml2pdf; Mermaid blocks rendered with @mermaid-js/mermaid-cli."""
import base64
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
from io import StringIO

import markdown
from xhtml2pdf import pisa

MD_NAME = "CA-Corrective-Actions-Unified-Spec.md"
MERMAID_FENCE = re.compile(r"```mermaid\s*\n(.*?)```", re.DOTALL)
MERMAID_HTML = re.compile(
    r'<pre><code class="language-mermaid">[\s\S]*?</code></pre>',
    re.IGNORECASE,
)


def extract_mermaid_sources(markdown_text: str) -> list[str]:
    return [m.strip() for m in MERMAID_FENCE.findall(markdown_text)]


def render_mermaid_pngs(sources: list[str], work_dir: pathlib.Path) -> list[bytes | None]:
    """Run mmdc for each diagram; return PNG bytes (None if render failed)."""
    results: list[bytes | None] = []
    for i, src in enumerate(sources):
        mmd = work_dir / f"diagram_{i}.mmd"
        png = work_dir / f"diagram_{i}.png"
        mmd.write_text(src + "\n", encoding="utf-8")
        # shell=True: on Windows, npx is npx.cmd; list-form subprocess often raises WinError 2.
        mi = str(mmd.resolve())
        po = str(png.resolve())
        cmd = (
            f'npx -y @mermaid-js/mermaid-cli -i "{mi}" -o "{po}" -b white -w 1400'
        )
        try:
            proc = subprocess.run(
                cmd,
                shell=True,
                capture_output=True,
                text=True,
                timeout=240,
                cwd=str(work_dir),
            )
        except (subprocess.TimeoutExpired, FileNotFoundError) as e:
            print(f"Mermaid diagram {i + 1}: skipped ({e})", file=sys.stderr)
            results.append(None)
            continue
        if proc.returncode != 0 or not png.is_file():
            print(
                f"Mermaid diagram {i + 1}: mmdc failed\n{proc.stderr or proc.stdout}",
                file=sys.stderr,
            )
            results.append(None)
            continue
        results.append(png.read_bytes())
    return results


def embed_mermaid_images(html_body: str, png_blobs: list[bytes | None]) -> str:
    """Replace each language-mermaid <pre> with <img> (base64) or keep original on failure."""

    def replacer(match: re.Match[str]) -> str:
        replacer.idx += 1  # type: ignore[attr-defined]
        i = replacer.idx - 1
        if i >= len(png_blobs) or png_blobs[i] is None:
            return match.group(0)
        data = base64.b64encode(png_blobs[i]).decode("ascii")
        return (
            '<p class="mermaid-wrap" style="margin:14px 0;text-align:center;">'
            f'<img src="data:image/png;base64,{data}" alt="Flow diagram" '
            'style="max-width:100%;height:auto;"/></p>'
        )

    replacer.idx = 0  # type: ignore[attr-defined]
    return MERMAID_HTML.sub(replacer, html_body)


def main() -> None:
    base = pathlib.Path(__file__).resolve().parent
    md_path = base / MD_NAME
    pdf_path = base / "CA-Corrective-Actions-Unified-Spec.pdf"

    text = md_path.read_text(encoding="utf-8")
    mermaid_sources = extract_mermaid_sources(text)

    png_blobs: list[bytes | None] = []
    tmp_dir: pathlib.Path | None = None
    if mermaid_sources:
        tmp_dir = pathlib.Path(tempfile.mkdtemp(prefix="ca_spec_mermaid_"))
        try:
            png_blobs = render_mermaid_pngs(mermaid_sources, tmp_dir)
        finally:
            if tmp_dir and tmp_dir.is_dir():
                shutil.rmtree(tmp_dir, ignore_errors=True)

    body = markdown.markdown(
        text,
        extensions=["tables", "fenced_code", "sane_lists", "nl2br"],
    )
    if mermaid_sources:
        n_html = len(MERMAID_HTML.findall(body))
        if n_html != len(mermaid_sources):
            print(
                f"Warning: {len(mermaid_sources)} mermaid fences in MD but "
                f"{n_html} language-mermaid blocks in HTML — diagram order may be wrong.",
                file=sys.stderr,
            )
        body = embed_mermaid_images(body, png_blobs)

    footer_note = (
        f"Source: {MD_NAME}. Mermaid diagrams rendered as images for PDF."
        if mermaid_sources
        else f"Source: {MD_NAME}."
    )
    full_html = f"""<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<title>CA Corrective Actions — Unified spec</title>
<style>
@page {{ size: A4; margin: 15mm; }}
body {{ font-family: Helvetica, Arial, sans-serif; font-size: 11pt; line-height: 1.45;
  color: #222; }}
h1 {{ font-size: 18pt; border-bottom: 1px solid #ccc; padding-bottom: 8px; }}
h2 {{ font-size: 14pt; margin-top: 1.1em; }}
h3 {{ font-size: 12pt; margin-top: 0.9em; }}
table {{ border-collapse: collapse; width: 100%; margin: 10px 0; font-size: 10pt; }}
th, td {{ border: 1px solid #ccc; padding: 5px 7px; text-align: left; vertical-align: top; }}
th {{ background: #f0f0f0; }}
code {{ font-family: Courier, monospace; font-size: 9pt; }}
pre {{ background: #f5f5f5; padding: 8px; white-space: pre-wrap; word-wrap: break-word;
  font-size: 8.5pt; }}
pre code {{ font-size: 8.5pt; }}
hr {{ border: none; border-top: 1px solid #ddd; margin: 18px 0; }}
ul {{ margin: 6px 0; }}
a {{ color: #006644; }}
</style></head><body>
{body}
<p style="margin-top:2em;color:#666;font-size:9pt;">{footer_note}</p>
</body></html>"""

    with open(pdf_path, "wb") as pdf_file:
        result = pisa.CreatePDF(StringIO(full_html), dest=pdf_file, encoding="utf-8")

    if result.err:
        sys.exit("xhtml2pdf reported errors; PDF may be incomplete.")

    header = pdf_path.read_bytes()[:5]
    if header != b"%PDF-":
        sys.exit(f"Output is not a valid PDF (starts with {header!r}).")

    print(f"Wrote: {pdf_path}", file=sys.stderr)


if __name__ == "__main__":
    main()
