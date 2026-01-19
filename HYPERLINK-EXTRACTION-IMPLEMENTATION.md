# Hyperlink Extraction Implementation

## Overview
This implementation adds hyperlink extraction from CVs to enhance the LLM context when generating cover letters and customized CVs.

## Changes Made

### 1. Domain Layer (`src/CoverLetter.Domain/Entities/CvDocument.cs`)
**Added new domain entities:**
- `Hyperlink` record: Stores extracted hyperlinks with URL, display text, and type
- `HyperlinkType` enum: Categorizes links (Email, LinkedIn, GitHub, Portfolio, General, Other)
- Updated `CvDocument` entity:
  - Added `Hyperlinks` property of type `IReadOnlyList<Hyperlink>`
  - Updated constructor to accept hyperlinks
  - Updated `Create` factory method with optional `hyperlinks` parameter

### 2. Infrastructure Layer (`src/CoverLetter.Infrastructure/CvParsers/CvParserService.cs`)
**Enhanced PDF parsing:**
- Modified `ParsePdfAsync` to extract hyperlinks during PDF parsing
- Added `ExtractHyperlinksFromPage` method:
  - Extracts hyperlinks from PDF annotations (clickable links)
  - Extracts URLs from text content using regex patterns
  - Supports email addresses (adds mailto: prefix)
  - Supports HTTP/HTTPS URLs
  - Deduplicates URLs
- Added `CategorizeUrl` method:
  - Intelligently categorizes URLs by domain/pattern
  - Recognizes LinkedIn, GitHub, portfolio sites, and email addresses
- Updated logging to include hyperlink count

### 3. Application Layer - CV Customization (`src/CoverLetter.Application/UseCases/CustomizeCv/CustomizeCvHandler.cs`)
**Enhanced prompt building:**
- Added hyperlink section to the prompt variables
- Formats hyperlinks with type description and URL
- Includes display text when available
- Appends formatted hyperlinks to `CvText` variable
- Example format:
  ```
  **HYPERLINKS FROM CV**: The following links must be preserved in the customized CV:
  - Email: mailto:john@example.com
  - LinkedIn Profile: https://linkedin.com/in/john
  - GitHub Profile: https://github.com/john
  ```

### 4. Application Layer - Cover Letter Generation (`src/CoverLetter.Application/UseCases/GenerateCoverLetter/GenerateCoverLetterHandler.cs`)
**Enhanced CV text resolution:**
- Modified `ResolveCvTextAsync` to include hyperlinks in CV text
- Appends formatted "Contact Links" section to CV text
- Ensures LLM has access to contact information for cover letters

### 5. Prompt Registry (`src/CoverLetter.Application/Common/Services/PromptRegistry.cs`)
**Updated CV customization prompt:**
- Added hyperlink handling guidelines
- Changed from "NO WEB LINKS" to proper LaTeX hyperlink instructions
- Added rule #4: Use `\href{URL}{display text}` for clickable links
- Added rule: Use `\url{URL}` for plain URLs
- Added instruction #6: "HYPERLINK PRESERVATION" for header links
- Includes mailto: format guidance for emails

## Technical Details

### Hyperlink Extraction Sources
1. **PDF Annotations**: Extracts from link annotations with URI actions
2. **Text Pattern Matching**:
   - Email regex: `\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b`
   - URL regex: `\bhttps?://[^\s<>""']+\b`

### URL Categorization Logic
- Email: Contains "@" or starts with "mailto:"
- LinkedIn: Contains "linkedin.com"
- GitHub: Contains "github.com"
- Portfolio: Contains "portfolio", "behance.com", or "dribbble.com"
- General: All other URLs

### LaTeX Output Format
The LLM is instructed to use:
- `\href{mailto:email@example.com}{email@example.com}` for emails
- `\href{https://linkedin.com/in/user}{LinkedIn Profile}` for social links
- `\url{https://example.com}` for plain URLs

## Benefits

1. **Complete Information**: LLM has access to all contact links from the original CV
2. **Preservation**: Professional links (LinkedIn, GitHub, portfolio) are maintained in customized CVs
3. **Context**: Cover letters can reference or mention professional profiles
4. **Smart Categorization**: Links are labeled by type for better prompt context
5. **Production-Grade**: Handles edge cases like malformed annotations and duplicate URLs

## Usage

The feature is automatic and requires no API changes:
1. Upload a CV with hyperlinks (email, URLs in text, or clickable PDF links)
2. Generate a cover letter or customize CV
3. Hyperlinks are automatically extracted and included in LLM prompts
4. Customized CVs will preserve contact links in LaTeX format

## Testing Recommendations

1. **Test with various PDF types:**
   - CVs with clickable link annotations
   - CVs with plain text URLs
   - CVs with email addresses
   - CVs with multiple link types

2. **Verify extraction:**
   - Check logs for hyperlink count after parsing
   - Verify all link types are categorized correctly

3. **Validate LLM output:**
   - Ensure customized CVs include `\href{}` commands
   - Verify emails have mailto: prefix
   - Confirm links are clickable in compiled PDF

## Future Enhancements

1. Extract hyperlinks from LaTeX CVs (parse `\href{}` and `\url{}` commands)
2. Extract hyperlinks from plain text using URL detection
3. Add hyperlink validation (check if URLs are reachable)
4. Support custom display text preferences
5. Add API endpoint to view extracted hyperlinks separately
