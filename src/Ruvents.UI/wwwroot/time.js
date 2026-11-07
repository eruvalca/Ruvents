// The browser's own time zone. Reading it here rather than from the .NET host keeps
// server-rendered pages from showing the server's zone instead of the viewer's.
export function getTimeZone() {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone ?? '';
    } catch {
        return '';
    }
}
