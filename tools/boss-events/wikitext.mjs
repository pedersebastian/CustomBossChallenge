// A small reader for the MediaWiki templates on Blooncyclopedia's event history pages, e.g.
//   {{BTD6 boss event rules|id=...|normal rules={{BTD6 challenge rules|_towers={{BTD6 restrictions|tower=...}}...}}}}
// Templates nest, so the text is split on | and = only outside inner {{ }}.

/** Every top-level {{template}} in the text, as { name, params, text }, whose params are strings by name. */
export function templates(text) {
    const found = [];
    let depth = 0;
    let start = -1;
    for (let i = 0; i < text.length - 1; i++) {
        if (text[i] === '{' && text[i + 1] === '{') {
            if (depth++ === 0) start = i;
            i++;
        } else if (text[i] === '}' && text[i + 1] === '}' && depth > 0) {
            if (--depth === 0) found.push(parseTemplate(text.slice(start + 2, i)));
            i++;
        }
    }
    return found;
}

/** The top-level {{templates}} named `name` (case and spaces as on the wiki). */
export function templatesNamed(text, name) {
    return templates(text).filter(template => template.name === name);
}

function parseTemplate(inner) {
    const parts = splitTopLevel(inner, '|');
    const params = {};
    for (const part of parts.slice(1)) {
        const equals = indexOfTopLevel(part, '=');
        // Positional params aren't used by these templates; keep them under their number like MediaWiki does
        if (equals < 0) params[Object.keys(params).length + 1] = part.trim();
        // A repeated name keeps the first value: the event templates repeat `id` and `type` inside the rules
        else {
            const key = part.slice(0, equals).trim();
            if (!(key in params)) params[key] = part.slice(equals + 1).trim();
        }
    }
    return { name: parts[0].trim(), params, text: inner };
}

function splitTopLevel(text, separator) {
    const parts = [];
    let depth = 0;
    let last = 0;
    for (let i = 0; i < text.length; i++) {
        if (text.startsWith('{{', i)) {
            depth++;
            i++;
        } else if (text.startsWith('}}', i)) {
            depth--;
            i++;
        } else if (depth === 0 && text[i] === separator) {
            parts.push(text.slice(last, i));
            last = i + 1;
        }
    }
    parts.push(text.slice(last));
    return parts;
}

function indexOfTopLevel(text, char) {
    let depth = 0;
    for (let i = 0; i < text.length; i++) {
        if (text.startsWith('{{', i)) {
            depth++;
            i++;
        } else if (text.startsWith('}}', i)) {
            depth--;
            i++;
        } else if (depth === 0 && text[i] === char) return i;
    }
    return -1;
}
