'use strict';

function bytesToBase64(bytes) {
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

function base64ToBytes(base64) {
    return Uint8Array.from(atob(base64), c => c.charCodeAt(0));
}

export async function signRsa(pkcs8Base64, dataBase64) {
    const keyBytes = base64ToBytes(pkcs8Base64);
    const dataBytes = base64ToBytes(dataBase64);
    const key = await crypto.subtle.importKey(
        'pkcs8',
        keyBytes,
        { name: 'RSASSA-PKCS1-v1_5', hash: { name: 'SHA-256' } },
        false,
        ['sign']
    );
    const signature = await crypto.subtle.sign('RSASSA-PKCS1-v1_5', key, dataBytes);
    return bytesToBase64(new Uint8Array(signature));
}

export async function verifyRsa(spkiBase64, signatureBase64, dataBase64) {
    const keyBytes = base64ToBytes(spkiBase64);
    const signatureBytes = base64ToBytes(signatureBase64);
    const dataBytes = base64ToBytes(dataBase64);
    const key = await crypto.subtle.importKey(
        'spki',
        keyBytes,
        { name: 'RSASSA-PKCS1-v1_5', hash: { name: 'SHA-256' } },
        false,
        ['verify']
    );
    return await crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, signatureBytes, dataBytes);
}
