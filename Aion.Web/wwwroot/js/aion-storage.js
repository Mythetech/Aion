const DB_NAME = 'aion-storage';
const DB_VERSION = 1;

let dbPromise = null;

function openDb() {
    if (dbPromise) return dbPromise;

    dbPromise = new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains('connections')) {
                db.createObjectStore('connections', { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains('queries')) {
                db.createObjectStore('queries', { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains('databases')) {
                db.createObjectStore('databases', { keyPath: 'name' });
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });

    return dbPromise;
}

export async function requestPersistence() {
    if (navigator.storage && navigator.storage.persist) {
        await navigator.storage.persist();
    }
}

export async function saveConnection(connectionJson) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('connections', 'readwrite');
        tx.objectStore('connections').put(JSON.parse(connectionJson));
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function loadConnections() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('connections', 'readonly');
        const request = tx.objectStore('connections').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result));
        request.onerror = () => reject(request.error);
    });
}

export async function deleteConnection(id) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('connections', 'readwrite');
        tx.objectStore('connections').delete(id);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function saveQuery(queryJson) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('queries', 'readwrite');
        tx.objectStore('queries').put(JSON.parse(queryJson));
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function loadQueries() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('queries', 'readonly');
        const request = tx.objectStore('queries').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result));
        request.onerror = () => reject(request.error);
    });
}

export async function deleteQuery(id) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('queries', 'readwrite');
        tx.objectStore('queries').delete(id);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function saveDatabaseMeta(metaJson) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('databases', 'readwrite');
        tx.objectStore('databases').put(JSON.parse(metaJson));
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function loadDatabaseMetas() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('databases', 'readonly');
        const request = tx.objectStore('databases').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result));
        request.onerror = () => reject(request.error);
    });
}

export async function deleteDatabaseMeta(name) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('databases', 'readwrite');
        tx.objectStore('databases').delete(name);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function clearAll() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const storeNames = ['connections', 'queries', 'databases'];
        const tx = db.transaction(storeNames, 'readwrite');
        for (const name of storeNames) {
            tx.objectStore(name).clear();
        }
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}
