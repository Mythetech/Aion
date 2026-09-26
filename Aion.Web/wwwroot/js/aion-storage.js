const DB_NAME = 'aion-storage';
// Version 3 re-runs the upgrade for databases created by builds that added only one of the settings
// and history stores at version 2; every store creation below is guarded, so re-running is safe.
const DB_VERSION = 3;

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
            if (!db.objectStoreNames.contains('settings')) {
                db.createObjectStore('settings', { keyPath: 'settingsId' });
            }
            if (!db.objectStoreNames.contains('history')) {
                db.createObjectStore('history', { keyPath: 'id' });
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

export async function saveSettings(settingsId, json) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('settings', 'readwrite');
        tx.objectStore('settings').put({ settingsId, json, updatedAt: new Date().toISOString() });
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function replaceHistory(entriesJson) {
    const db = await openDb();
    const entries = JSON.parse(entriesJson);
    return new Promise((resolve, reject) => {
        const tx = db.transaction('history', 'readwrite');
        const store = tx.objectStore('history');
        store.clear();
        for (const entry of entries) {
            store.put(entry);
        }
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

export async function loadSettings(settingsId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('settings', 'readonly');
        const request = tx.objectStore('settings').get(settingsId);
        request.onsuccess = () => resolve(request.result ? request.result.json : null);
        request.onerror = () => reject(request.error);
    });
}

export async function loadAllSettings() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('settings', 'readonly');
        const request = tx.objectStore('settings').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result.map(r => ({ settingsId: r.settingsId, json: r.json }))));
        request.onerror = () => reject(request.error);
    });
}

export async function loadHistory() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction('history', 'readonly');
        const request = tx.objectStore('history').getAll();
        request.onsuccess = () => resolve(JSON.stringify(request.result));
        request.onerror = () => reject(request.error);
    });
}

export async function clearAll() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const storeNames = ['connections', 'queries', 'databases', 'history'];
        const tx = db.transaction(storeNames, 'readwrite');
        for (const name of storeNames) {
            tx.objectStore(name).clear();
        }
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}
