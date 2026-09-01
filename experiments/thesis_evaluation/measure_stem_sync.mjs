import { spawn } from 'node:child_process';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';


const root = path.dirname(new URL(import.meta.url).pathname.replace(/^\/(.:)/, '$1'));
const resultsDirectory = path.join(root, 'results');
const chromePath = process.env.CHROME_BIN ?? 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const baseUrl = process.env.THESIS_BASE_URL ?? 'http://localhost:7000';
const username = process.env.THESIS_USERNAME;
const password = process.env.THESIS_PASSWORD;
const trackId = Number(process.env.THESIS_TRACK_ID ?? 17);

if (!username || !password) {
  throw new Error('THESIS_USERNAME and THESIS_PASSWORD must be provided.');
}

await fs.mkdir(resultsDirectory, {recursive: true});


async function login() {
  const response = await fetch(`${baseUrl}/api/UserAuthLoginEndpoint`, {
    method: 'POST',
    headers: {'content-type': 'application/json'},
    body: JSON.stringify({username, password})
  });
  if (!response.ok) {
    throw new Error(`Login failed with HTTP ${response.status}.`);
  }
  return response.json();
}


async function loadStemUrls(token) {
  const response = await fetch(`${baseUrl}/api/v2/tracks/${trackId}/playback?artistMode=true`, {
    headers: {authorization: `Bearer ${token}`}
  });
  if (!response.ok) {
    throw new Error(`Playback manifest failed with HTTP ${response.status}.`);
  }
  const manifest = await response.json();
  const assets = manifest?.stream?.stemSet?.stems ?? [];
  if (assets.length !== 4 || assets.some(asset => !asset.url)) {
    throw new Error(`Expected four signed stem assets, got ${assets.length}.`);
  }
  return assets.map(asset => asset.url);
}


async function waitForDebuggingEndpoint(port, attempts = 100) {
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    try {
      const response = await fetch(`http://127.0.0.1:${port}/json/list`);
      if (response.ok) {
        const targets = await response.json();
        const page = targets.find(target => target.type === 'page');
        if (page?.webSocketDebuggerUrl) {
          return page;
        }
      }
    }
    catch {
      // Chrome is still starting.
    }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Chrome DevTools endpoint did not become available.');
}


class CdpClient {
  constructor(url) {
    this.socket = new WebSocket(url);
    this.nextId = 1;
    this.pending = new Map();
    this.ready = new Promise((resolve, reject) => {
      this.socket.addEventListener('open', resolve, {once: true});
      this.socket.addEventListener('error', reject, {once: true});
    });
    this.socket.addEventListener('message', event => {
      const message = JSON.parse(event.data);
      if (!message.id || !this.pending.has(message.id)) {
        return;
      }
      const {resolve, reject} = this.pending.get(message.id);
      this.pending.delete(message.id);
      if (message.error) {
        reject(new Error(JSON.stringify(message.error)));
      }
      else {
        resolve(message.result);
      }
    });
  }

  async send(method, params = {}) {
    await this.ready;
    const id = this.nextId++;
    const result = new Promise((resolve, reject) => this.pending.set(id, {resolve, reject}));
    this.socket.send(JSON.stringify({id, method, params}));
    return result;
  }

  close() {
    this.socket.close();
  }
}


const browserMeasureFunction = String.raw`
async function measureStemPlayback(urls, options) {
  document.body.innerHTML = '';
  const audios = urls.map((url, index) => {
    const audio = document.createElement('audio');
    audio.preload = 'auto';
    audio.muted = true;
    audio.dataset.index = String(index);
    audio.src = url;
    document.body.appendChild(audio);
    return audio;
  });

  const waitUntilReady = audio => new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Audio readiness timeout')), 30000);
    const ready = () => {
      clearTimeout(timer);
      resolve();
    };
    const failed = () => {
      clearTimeout(timer);
      reject(new Error('Audio element failed: ' + (audio.error?.message ?? 'unknown')));
    };
    if (audio.readyState >= HTMLMediaElement.HAVE_FUTURE_DATA) {
      ready();
      return;
    }
    audio.addEventListener('canplay', ready, {once: true});
    audio.addEventListener('error', failed, {once: true});
    audio.load();
  });

  const loadStarted = performance.now();
  await Promise.all(audios.map(waitUntilReady));
  const startupLoadMs = performance.now() - loadStarted;
  const leader = audios[0];
  let corrections = 0;
  const thresholdSeconds = 0.08;
  const synchronize = () => {
    for (const follower of audios.slice(1)) {
      if (Math.abs(follower.currentTime - leader.currentTime) > thresholdSeconds) {
        follower.currentTime = leader.currentTime;
        corrections += 1;
      }
    }
  };
  leader.addEventListener('timeupdate', synchronize);

  for (const audio of audios) {
    audio.currentTime = 0;
  }
  const playStarted = performance.now();
  await Promise.all(audios.map(audio => audio.play()));
  const playPromiseMs = performance.now() - playStarted;

  const samples = [];
  const started = performance.now();
  let actionDone = false;
  while (performance.now() - started < options.durationMs) {
    const elapsed = performance.now() - started;
    if (!actionDone && options.action === 'seek' && elapsed >= options.actionAtMs) {
      const target = Math.min(15, Math.max(1, leader.duration - 3));
      for (const audio of audios) {
        audio.currentTime = target;
      }
      actionDone = true;
    }
    if (!actionDone && options.action === 'pause-resume' && elapsed >= options.actionAtMs) {
      audios.forEach(audio => audio.pause());
      await new Promise(resolve => setTimeout(resolve, options.pauseMs));
      await Promise.all(audios.map(audio => audio.play()));
      actionDone = true;
    }
    if (!actionDone && options.action === 'inject-drift' && elapsed >= options.actionAtMs) {
      audios[1].currentTime = leader.currentTime + 0.25;
      actionDone = true;
    }

    const leaderTime = leader.currentTime;
    for (const follower of audios.slice(1)) {
      samples.push(Math.abs(follower.currentTime - leaderTime) * 1000);
    }
    await new Promise(resolve => setTimeout(resolve, 50));
  }

  audios.forEach(audio => audio.pause());
  leader.removeEventListener('timeupdate', synchronize);
  samples.sort((left, right) => left - right);
  const percentile = value => {
    if (!samples.length) return 0;
    return samples[Math.min(samples.length - 1, Math.ceil(value * samples.length) - 1)];
  };
  return {
    scenario: options.name,
    browser: navigator.userAgent,
    sampleCount: samples.length,
    startupLoadMs,
    playPromiseMs,
    medianDriftMs: percentile(0.5),
    p95DriftMs: percentile(0.95),
    maximumDriftMs: samples.at(-1) ?? 0,
    corrections,
    correctionsPerHour: corrections / (options.durationMs / 3600000),
    thresholdMs: thresholdSeconds * 1000,
    actionPerformed: actionDone
  };
}`;


async function evaluateScenario(client, urls, options) {
  const expression = `(${browserMeasureFunction})(${JSON.stringify(urls)}, ${JSON.stringify(options)})`;
  const response = await client.send('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
    userGesture: true
  });
  if (response.exceptionDetails) {
    throw new Error(JSON.stringify(response.exceptionDetails));
  }
  return response.result.value;
}


const loginResult = await login();
const urls = await loadStemUrls(loginResult.token);
const port = 9325;
const temporaryProfile = await fs.mkdtemp(path.join(os.tmpdir(), '808music-sync-'));
const chrome = spawn(chromePath, [
  '--headless=new',
  '--no-sandbox',
  '--disable-gpu',
  '--disable-dev-shm-usage',
  '--autoplay-policy=no-user-gesture-required',
  `--remote-debugging-port=${port}`,
  `--user-data-dir=${temporaryProfile}`,
  'about:blank'
], {stdio: 'ignore'});

let client;
try {
  const page = await waitForDebuggingEndpoint(port);
  client = new CdpClient(page.webSocketDebuggerUrl);
  await client.send('Runtime.enable');
  await client.send('Network.enable');

  const scenarios = [];
  scenarios.push(await evaluateScenario(client, urls, {
    name: 'kontinuirana reprodukcija',
    durationMs: 15000,
    action: 'none'
  }));
  scenarios.push(await evaluateScenario(client, urls, {
    name: 'seek i nastavak',
    durationMs: 15000,
    action: 'seek',
    actionAtMs: 5000
  }));

  await client.send('Network.setCacheDisabled', {cacheDisabled: true});
  await client.send('Network.emulateNetworkConditions', {
    offline: false,
    latency: 150,
    downloadThroughput: 750 * 1024 / 8,
    uploadThroughput: 250 * 1024 / 8,
    connectionType: 'cellular3g'
  });
  scenarios.push(await evaluateScenario(client, urls, {
    name: 'ograničena mreža',
    durationMs: 15000,
    action: 'none'
  }));
  await client.send('Network.emulateNetworkConditions', {
    offline: false,
    latency: 0,
    downloadThroughput: -1,
    uploadThroughput: -1,
    connectionType: 'none'
  });
  scenarios.push(await evaluateScenario(client, urls, {
    name: 'prekid i nastavak reprodukcije',
    durationMs: 18000,
    action: 'pause-resume',
    actionAtMs: 5000,
    pauseMs: 3000
  }));
  scenarios.push(await evaluateScenario(client, urls, {
    name: 'kontrolirano izazvani odmak',
    durationMs: 8000,
    action: 'inject-drift',
    actionAtMs: 2500
  }));

  const payload = {
    testedAtUtc: new Date().toISOString(),
    trackId,
    stemCount: urls.length,
    implementationThresholdMs: 80,
    samplingIntervalMs: 50,
    note: 'Headless Chrome; fourth scenario simulates interruption/resume, not OS background throttling.',
    scenarios
  };
  await fs.writeFile(
    path.join(resultsDirectory, 'stem_sync_browser.json'),
    JSON.stringify(payload, null, 2),
    'utf8'
  );
  console.log(JSON.stringify(payload, null, 2));
}
finally {
  client?.close();
  const chromeExited = new Promise(resolve => chrome.once('exit', resolve));
  chrome.kill();
  await Promise.race([chromeExited, new Promise(resolve => setTimeout(resolve, 3000))]);
  const resolvedTemporaryProfile = path.resolve(temporaryProfile);
  const resolvedTempRoot = path.resolve(os.tmpdir());
  if (path.dirname(resolvedTemporaryProfile) === resolvedTempRoot && path.basename(resolvedTemporaryProfile).startsWith('808music-sync-')) {
    try {
      await fs.rm(resolvedTemporaryProfile, {recursive: true, force: true, maxRetries: 5, retryDelay: 200});
    }
    catch (error) {
      console.error(`Temporary Chrome profile could not be removed: ${error.message}`);
    }
  }
}
