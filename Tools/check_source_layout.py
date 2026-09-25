#!/usr/bin/env python3
"""Small lexical / asset audit, NOT a compiler. Does not prove C# or shader correctness."""
import re,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def strip_literals(text):
    # This project's C# has ordinary/verbatim strings, char literals and comments, not raw strings.
    pattern=r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/'
    return re.sub(pattern,lambda m:' '*len(m.group()),text)

report=[]
for path in sorted((ROOT/'Assets/InFalsusStudio').rglob('*')):
    if path.suffix not in ('.cs','.shader'):continue
    text=path.read_text();clean=strip_literals(text);stack=[];pairs={')':'(',']':'[','}':'{'}
    for ch in clean:
        if ch in '([{':stack.append(ch)
        elif ch in ')]}':
            assert stack and stack.pop()==pairs[ch],f'Unbalanced delimiters: {path}'
    assert not stack,f'Unclosed delimiter: {path}'
    if path.suffix=='.cs':
        assert len(re.findall(r'^\s*#if\b',text,re.M))==len(re.findall(r'^\s*#endif\b',text,re.M)),f'Preprocessor count: {path}'
        # A frequent C# issue: `var a=..., b=...;` is illegal. Ignore nested constructor args.
        # Commas in generic type arguments (Dictionary<string,object>) are not
        # multiple declarators. This remains a finite lexical heuristic, not a parser.
        decl_clean=re.sub(r'\b[A-Z][A-Za-z0-9_.]*<[A-Za-z0-9_.,?\s\[\]<>]+>',
                          lambda m:m.group().replace(',', ' '),clean)
        for m in re.finditer(r'\bvar\s+\w+\s*=',decl_clean):
            depth=0
            for ch in decl_clean[m.end():]:
                if ch in '([{':depth+=1
                elif ch in ')]}':
                    if depth == 0: break  # End of a using(var ...) or equivalent scoped initializer.
                    depth-=1
                elif ch==';' and depth==0:break
                elif ch==',' and depth==0:raise AssertionError(f'Multiple var declarators: {path}')
    report.append({'file':str(path.relative_to(ROOT)),'lexical_check':'passed'})
(ROOT/'Documentation/Reference/source_layout_check.json').write_text(json.dumps({'scope':'Lexical delimiters / common declaration check only, not C# or Shader compilation','files':report},indent=2))
print(f'{len(report)} source files passed limited lexical checks. This is NOT compilation.')
