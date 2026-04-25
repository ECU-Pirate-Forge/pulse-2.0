export function setupDragAndDrop(container) {
    if (!container) return;

    const items = container.querySelectorAll('[draggable="true"]');
    
    items.forEach(item => {
        item.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.stopPropagation();
            e.dataTransfer.dropEffect = 'move';
            item.classList.add('drag-over');
        });

        item.addEventListener('dragleave', (e) => {
            e.preventDefault();
            e.stopPropagation();
            item.classList.remove('drag-over');
        });

        item.addEventListener('drop', (e) => {
            e.preventDefault();
            e.stopPropagation();
            item.classList.remove('drag-over');
        });

        item.addEventListener('dragstart', (e) => {
            e.dataTransfer.effectAllowed = 'move';
            item.classList.add('dragging');
        });

        item.addEventListener('dragend', (e) => {
            item.classList.remove('dragging');
            items.forEach(i => i.classList.remove('drag-over'));
        });
    });
}
