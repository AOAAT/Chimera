import argparse
import os
import re
import xml.etree.ElementTree as ET
import zipfile

NS = {
    "a": "http://schemas.openxmlformats.org/drawingml/2006/main",
    "p": "http://schemas.openxmlformats.org/presentationml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
    "pr": "http://schemas.openxmlformats.org/package/2006/relationships",
}


def natural_key(name):
    return [int(x) if x.isdigit() else x for x in re.split(r"(\d+)", name)]


def clean(s):
    return re.sub(r"\s+", " ", s or "").strip()


def text_from_xml(data):
    root = ET.fromstring(data)
    chunks = []
    for para in root.findall(".//a:p", NS):
        text = clean("".join((node.text or "") for node in para.findall(".//a:t", NS)))
        if text:
            chunks.append(text)
    return chunks


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("pptx")
    ap.add_argument("out")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    with zipfile.ZipFile(args.pptx) as z:
        names = set(z.namelist())
        slides = sorted(
            [n for n in names if re.fullmatch(r"ppt/slides/slide\d+\.xml", n)],
            key=natural_key,
        )
        output = [f"FILE: {args.pptx}", f"SLIDES: {len(slides)}"]
        for idx, slide_name in enumerate(slides, 1):
            output.extend(["", f"===== SLIDE {idx} ====="])
            output.extend(text_from_xml(z.read(slide_name)))
            rel_name = slide_name.replace("slides/slide", "slides/_rels/slide") + ".rels"
            if rel_name in names:
                rel_root = ET.fromstring(z.read(rel_name))
                links = []
                for rel in rel_root.findall("pr:Relationship", NS):
                    target = rel.attrib.get("Target", "")
                    if "media/" in target:
                        links.append(os.path.basename(target))
                if links:
                    output.append("[MEDIA] " + ", ".join(links))
        with open(os.path.join(args.out, "content.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(output))


if __name__ == "__main__":
    main()
