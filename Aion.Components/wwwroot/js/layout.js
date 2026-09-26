export function getElementWidth(selector, fallback) {
    const element = document.querySelector(selector);
    return element ? Math.round(element.getBoundingClientRect().width) : fallback;
}

// Form fields keep app shortcuts from acting behind a dialog. The query editor is Monaco's hidden textarea,
// but it's where New Query is most often pressed, so it doesn't count.
export function isEditableElementFocused() {
    const element = document.activeElement;
    if (!element || element.closest('.monaco-editor')) return false;
    return element.tagName === 'INPUT' || element.tagName === 'TEXTAREA' || element.isContentEditable;
}
