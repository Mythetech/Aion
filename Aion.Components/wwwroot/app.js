    const resizer = document.querySelector('.resizer');
    const drawer = document.querySelector('.mud-drawer');
    const root = document.documentElement;

    resizer.addEventListener('mousedown', (e) => {
    e.preventDefault();

    const onMouseMove = (e) => {
    const newWidth = e.clientX - drawer.getBoundingClientRect().left;
    root.style.setProperty('--mud-drawer-width-left', `${newWidth}px`);
};

    const onMouseUp = () => {
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', onMouseUp);
};

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
});