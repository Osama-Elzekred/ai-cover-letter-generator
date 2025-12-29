FROM debian:bookworm-slim

# Install minimal TeX Live packages for CV generation
# latex-base: Core LaTeX
# latex-recommended: Common packages
# latex-extra: Extra packages like moderncv
# fonts-recommended: Standard fonts
# latexmk: Automation tool for compilation
RUN apt-get update && apt-get install -y --no-install-recommends \
    texlive-latex-base \
    texlive-latex-recommended \
    texlive-latex-extra \
    texlive-fonts-recommended \
    texlive-binaries \
    latexmk \
    ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# Use a non-root user for security
RUN useradd -m -u 10001 tex
USER tex
WORKDIR /work

# Default command: compile /work/main.tex -> /work/out/main.pdf
# -pdf: generate PDF
# -interaction=nonstopmode: don't stop on errors
# -halt-on-error: stop immediately if an error occurs
# -no-shell-escape: disable \write18 for security
# -outdir=out: put build artifacts in 'out' folder
CMD ["bash", "-lc", "mkdir -p out && latexmk -pdf -interaction=nonstopmode -halt-on-error -no-shell-escape -outdir=out main.tex"]
