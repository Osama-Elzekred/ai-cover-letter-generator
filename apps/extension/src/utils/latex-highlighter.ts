/**
 * LaTeX Syntax Highlighter - Shared utility for coloring LaTeX commands
 * Used in both popup editor and LinkedIn widget
 */

// LaTeX commands and environments to highlight
const LATEX_KEYWORDS = {
  // Document structure
  commands: [
    'documentclass', 'usepackage', 'pagestyle', 'geometry', 'inputenc', 
    'hyperref', 'enumitem', 'titlesec', 'xcolor', 'amssymb', 'ifthen',
    'newcommand', 'renewcommand', 'newenvironment', 'begin', 'end',
    'documentstyle', 'setlength', 'setcounter', 'addtolength',
    // Text formatting
    'textbf', 'textit', 'texttt', 'emph', 'large', 'Large', 'LARGE',
    'small', 'tiny', 'normalsize', 'textcolor', 'textwidth',
    // Sections
    'section', 'subsection', 'subsubsection', 'paragraph', 'subparagraph',
    'chapter', 'part', 'appendix',
    // Layout
    'centering', 'raggedright', 'raggedleft', 'justify', 'parindent',
    'vspace', 'hspace', 'newpage', 'clearpage', 'pagebreak',
    // Lists
    'itemize', 'enumerate', 'description', 'item', 'label', 'ref',
    // Math
    'equation', 'align', 'gather', 'displaystyle', 'textstyle',
    // Tables
    'tabular', 'table', 'caption', 'multirow', 'multicolumn',
    // Other common
    'author', 'date', 'title', 'maketitle', 'tableofcontents',
    'input', 'include', 'includegraphics', 'graphic', 'eps', 'pdf',
    'fboxsep', 'fboxrule', 'framebox', 'fbox',
  ],
  // Optional/bracket parameters
  delimiters: ['{', '}', '[', ']', '(', ')'],
  // Comment marker
  comment: '%',
};

/**
 * Highlight LaTeX keywords in code
 * Returns HTML string with colored spans
 */
export function highlightLatexCode(code: string): string {
  // Escape HTML
  let html = code
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // Highlight LaTeX commands (\commandname)
  html = html.replace(/\\([a-zA-Z]+)\*?/g, (match, cmd) => {
    if (LATEX_KEYWORDS.commands.includes(cmd)) {
      return `<span class="latex-cmd">${match}</span>`;
    }
    return match;
  });

  // Highlight special braces/brackets
  html = html.replace(/([{}[\]()])/g, '<span class="latex-delim">$1</span>');

  // Highlight comments (from % to end of line)
  html = html.replace(/(%.*?)(?=\n|$)/g, '<span class="latex-comment">$1</span>');

  // Highlight options in brackets/braces
  html = html.replace(/(\[)([^\]]+)(\])/g, (match) => {
    return match.replace(/=/g, '<span class="latex-option">=</span>');
  });

  return html;
}

/**
 * Update a highlighting container with colored LaTeX code
 * Used for both popup and LinkedIn widget editors
 */
export function updateLatexHighlighting(
  textarea: HTMLTextAreaElement,
  highlightContainer: HTMLElement
): void {
  let code = textarea.value;
  
  // Ensure code ends with space so final newline shows
  if (code[code.length - 1] === '\n') {
    code += ' ';
  }

  // Generate highlighted HTML
  const highlighted = highlightLatexCode(code);
  
  // Update container
  highlightContainer.innerHTML = `<code class="language-latex">${highlighted}</code>`;
}

/**
 * Sync scroll position between textarea and highlighting container
 */
export function syncScroll(
  textarea: HTMLTextAreaElement,
  highlightContainer: HTMLElement
): void {
  highlightContainer.scrollTop = textarea.scrollTop;
  highlightContainer.scrollLeft = textarea.scrollLeft;
}
