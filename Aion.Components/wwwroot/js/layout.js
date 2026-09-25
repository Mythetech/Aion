export function getElementWidth(selector, fallback) {
    const element = document.querySelector(selector);
    return element ? Math.round(element.getBoundingClientRect().width) : fallback;
}

export function isEditableElementFocused() {
    const element = document.activeElement;
    return !!element && (element.tagName === 'INPUT' || element.tagName === 'TEXTAREA' || element.isContentEditable);
}
