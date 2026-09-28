#!/usr/bin/env python3
"""Inyecta el manual de usuario oficial en el README.

El manual vive en un solo lugar: PasswordManager_.NET10/Resources/Raw/guide/
USER_GUIDE.md, que es el mismo archivo que carga la app. El README lo muestra
entre dos marcadores para que no pueda quedar una copia obsoleta dando vueltas.

Dos adaptaciones al contexto del README:

1. Las rutas de imagen. En el .md son relativas al archivo (doc01.jpg), pero en
   el README se resuelven contra la raiz del repo, donde no existe ningun
   doc01.jpg. Se reescriben a img/doc01.jpg, que es la copia que el README ya
   usaba.
2. Los encabezados. El manual trae su propio H1 y el README ya tiene dos, asi que
   se baja todo un nivel: H1 pasa a H2, H2 a H3, etc. Los bloques de codigo se
   respetan y no se tocan.

El reemplazo del bloque delimitado por los marcadores se hace por indices, no con
una expresion regular: la region's salida no depende de como estaba escrita la
entrada, asi que correr el script dos veces seguidas no produce diferencias. Eso
es lo que evita que el workflow entre en un ciclo de commits.

No requiere dependencias: solo la biblioteca estandar.
"""

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
GUIDE_PATH = REPO_ROOT / "PasswordManager_.NET10" / "Resources" / "Raw" / "guide" / "USER_GUIDE.md"
README_PATH = REPO_ROOT / "README.md"

START_MARKER = "<!-- MANUAL:INICIO - generado por .github/scripts/sync-manual.py, no editar a mano -->"
END_MARKER = "<!-- MANUAL:FIN -->"

# Solo src que no apuntan a una ruta ya preparada ni a un origen externo.
# IGNORECASE cubre capturas .PNG/.JPG; group(1) conserva el nombre original.
IMAGE_SRC = re.compile(
    r'src="((?!img/|https?://|data:)[^"]+\.(?:jpg|jpeg|png|gif|webp))"',
    re.IGNORECASE,
)

ATX_HEADING = re.compile(r"^(#{1,5})(\s)")


def demote_headings(text: str) -> str:
    """Baja un nivel cada encabezado ATX, sin tocar los bloques de codigo."""
    lines = []
    inside_fence = False

    for line in text.split("\n"):
        if line.lstrip().startswith("```"):
            inside_fence = not inside_fence
            lines.append(line)
            continue

        if not inside_fence and ATX_HEADING.match(line):
            line = "#" + line

        lines.append(line)

    return "\n".join(lines)


def build_body(markdown: str) -> str:
    body = IMAGE_SRC.sub(r'src="img/\1"', markdown)
    return demote_headings(body).rstrip()


def replace_region(readme: str, body: str) -> str:
    """Vuelca el cuerpo entre los dos marcadores de forma idempotente."""
    start = readme.index(START_MARKER)
    if start != 0 and readme[start - 1] != "\n":
        raise ValueError("El marcador de inicio debe empezar al principio de una linea.")

    end = readme.index(END_MARKER, start)

    head = readme[: readme.index("\n", start) + 1]
    tail = readme[end:]

    return f"{head}\n{body}\n\n{tail}"


def main() -> int:
    if not GUIDE_PATH.is_file():
        print(f"No se encontro el manual en {GUIDE_PATH}", file=sys.stderr)
        return 1

    # utf-8-sig descarta un eventual BOM; el proyecto usa UTF-8 sin BOM.
    readme = README_PATH.read_text(encoding="utf-8-sig")

    if START_MARKER not in readme or END_MARKER not in readme:
        print(
            "El README no tiene los marcadores del manual.\n"
            f"Agrega entre el titulo y el resto del README:\n\n{START_MARKER}\n{END_MARKER}",
            file=sys.stderr,
        )
        return 1

    try:
        updated = replace_region(readme, build_body(GUIDE_PATH.read_text(encoding="utf-8-sig")))
    except ValueError as error:
        print(str(error), file=sys.stderr)
        return 1

    if updated == readme:
        print("UNCHANGED")
        return 0

    # newline="\n" evita la traduccion a CRLF que hace Windows por defecto: el
    # repositorio guarda LF.
    with open(README_PATH, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(updated)

    print("CHANGED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
