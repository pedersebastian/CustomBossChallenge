// Writes a record as JSON that is easy to read and diff: indented, but objects without nested objects or arrays (a
// tower, the health multipliers) on one line, so a file is ~100 lines instead of ~700.

/** The record as the text of its file, ending with a newline. */
export function formatRecord(record) {
    return format(record, '') + '\n';
}

function format(value, indent) {
    if (Array.isArray(value)) {
        if (value.length === 0) return '[]';
        if (value.every(isFlat) && value.every(item => typeof item !== 'object' || item === null))
            return JSON.stringify(value).replaceAll(',', ', ');
        const inner = indent + '  ';
        return `[\n${value.map(item => inner + format(item, inner)).join(',\n')}\n${indent}]`;
    }
    if (value && typeof value === 'object') {
        const entries = Object.entries(value);
        if (entries.length === 0) return '{}';
        if (isFlat(value)) return `{ ${entries.map(([key, item]) => `${JSON.stringify(key)}: ${JSON.stringify(item)}`).join(', ')} }`;
        const inner = indent + '  ';
        return `{\n${entries.map(([key, item]) => `${inner}${JSON.stringify(key)}: ${format(item, inner)}`).join(',\n')}\n${indent}}`;
    }
    return JSON.stringify(value);
}

// An object or array of plain values only
function isFlat(value) {
    if (!value || typeof value !== 'object') return true;
    return Object.values(value).every(item => item === null || typeof item !== 'object');
}
