(function () {
  const API_BASE_KEY = 'patternixApiBaseUrl';
  const DATASET_KEY = 'patternixActiveDatasetId';
  let datasetCache = [];
  let inputDraftRows = [];
  let inputDraftEditIndex = -1;
  let inputCandidateDraftRows = [];
  let inputCandidateEditIndex = -1;

  function getQueryApiBaseUrl() {
    try {
      const url = new URL(window.location.href);
      const api = url.searchParams.get('api');
      if (api) {
        localStorage.setItem(API_BASE_KEY, api);
        return api;
      }
    } catch (_) {
      // ignore invalid URLs
    }
    return null;
  }

  function normalizeApiBaseUrl(url) {
    return String(url || '').trim().replace(/\/+$/, '');
  }

  function getApiBaseUrl() {
    const LIVE_API_BASE = 'https://patternix-api.onrender.com';
    const isFileProtocol = typeof window !== 'undefined' && window.location.protocol === 'file:';
    const isLocalHost =
      typeof window !== 'undefined' &&
      (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');
    const storedApi = localStorage.getItem(API_BASE_KEY) || '';
    if (window.PATTERNIX_API_BASE_URL) {
      return normalizeApiBaseUrl(window.PATTERNIX_API_BASE_URL);
    }
    const queryApi = getQueryApiBaseUrl();
    if (queryApi) {
      return normalizeApiBaseUrl(queryApi);
    }
    if (isFileProtocol || isLocalHost) {
      return normalizeApiBaseUrl(storedApi || 'http://127.0.0.1:5088');
    }
    return normalizeApiBaseUrl(LIVE_API_BASE + (storedApi.includes('loca.lt') ? '' : ''));
  }

  function setApiBaseUrl(url) {
    const next = String(url || '').trim().replace(/\/+$/, '');
    if (!next) {
      throw new Error('API base URL cannot be empty.');
    }
    localStorage.setItem(API_BASE_KEY, next);
    window.PATTERNIX_API_BASE_URL = next;
    return next;
  }

  function getActiveDatasetId() {
    return window.PATTERNIX_ACTIVE_DATASET_ID || localStorage.getItem(DATASET_KEY) || '';
  }

  function setActiveDatasetId(datasetId) {
    const id = datasetId ? String(datasetId) : '';
    window.PATTERNIX_ACTIVE_DATASET_ID = id;
    if (id) {
      localStorage.setItem(DATASET_KEY, id);
    } else {
      localStorage.removeItem(DATASET_KEY);
    }
    return id;
  }

  function setActiveDatasetName(name) {
    window.PATTERNIX_ACTIVE_DATASET_NAME = name || '';
    const titleInput = document.getElementById('datasetTitleInput');
    if (titleInput && typeof name === 'string') {
      titleInput.value = name;
    }
  }

  function setDatasetCache(datasets) {
    datasetCache = Array.isArray(datasets) ? datasets.slice() : [];
    window.PATTERNIX_DATASET_CACHE = datasetCache;
    return datasetCache;
  }

  async function apiRequest(path, options = {}) {
    const baseUrl = getApiBaseUrl();
    const response = await fetch(baseUrl + path, {
      method: options.method || 'GET',
      headers: {
        'Content-Type': 'application/json',
        ...(options.headers || {}),
      },
      body: options.body,
    });

    const contentType = response.headers.get('content-type') || '';
    const payload = contentType.includes('application/json')
      ? await response.json()
      : await response.text();

    if (!response.ok) {
      const message = typeof payload === 'string' ? payload : payload?.message || payload?.title || response.statusText;
      throw new Error(message || `Request failed (${response.status})`);
    }

    return payload;
  }

  function normalizeRow(apiRow, rowIndex) {
    return {
      id: apiRow.id,
      apiRowId: apiRow.id,
      no: String(apiRow.rowNo),
      left: apiRow.leftValue,
      rawLine: apiRow.rawLine || '',
      tuple: [apiRow.w, apiRow.x, apiRow.y, apiRow.z],
      isUnknown: apiRow.isUnknown,
      isLocked: !!apiRow.isLocked,
      isCurrent: !!apiRow.isUnknown && !apiRow.isLocked,
      candidates: (apiRow.candidates || []).map(tuple => tuple.slice()),
      rowIndex,
    };
  }

  function normalizeTheoryResult(apiTheory) {
    const local = Array.isArray(window.THEORIES) ? window.THEORIES.find(t => t.id === apiTheory.theoryCode) : null;
    return {
      id: apiTheory.theoryCode,
      theoryCode: apiTheory.theoryCode,
      name: apiTheory.name,
      group: apiTheory.groupName,
      groupName: apiTheory.groupName,
      desc: local?.desc || '',
      status: apiTheory.status,
      hits: apiTheory.hits,
      total: apiTheory.total,
      coverageScore: Number(apiTheory.coverageScore),
      confidence: Number(apiTheory.confidence),
      fwdRate: Number(apiTheory.forwardRate),
      revRate: Number(apiTheory.reverseRate),
      failures: apiTheory.failures || [],
    };
  }

  function normalizeCandidate(apiCandidate) {
    return {
      rank: apiCandidate.rank,
      rowNo: apiCandidate.rowNo,
      tuple: [apiCandidate.w, apiCandidate.x, apiCandidate.y, apiCandidate.z],
      confidence: Number(apiCandidate.confidence),
      rationale: apiCandidate.rationale,
      theories: apiCandidate.theories || [],
      evidence: (apiCandidate.evidence || []).map(item => ({
        type: item.type,
        text: item.text,
      })),
    };
  }

  function buildManualRowFromRequest(request) {
    return {
      id: null,
      apiRowId: null,
      no: String(request.rowNo ?? '?'),
      left: request.leftValue ?? 0,
      rawLine: '',
      tuple: [request.w ?? null, request.x ?? null, request.y ?? null, request.z ?? null],
      isUnknown: true,
      candidates: request.candidates || [],
      rowIndex: -1,
    };
  }

  function parseCsvTuple(text) {
    return String(text || '')
      .split(',')
      .map(part => part.trim())
      .map(part => (part === '' || part === '?') ? null : Number(part));
  }

  function parseNumericCell(value) {
    const text = String(value ?? '').trim();
    if (!text || text === '?') {
      return null;
    }

    const parsed = Number(text);
    return Number.isFinite(parsed) ? parsed : null;
  }

  function candidateTupleToText(tuple) {
    return (tuple || []).map(value => value === null || value === undefined || value === '' ? '?' : value).join(', ');
  }

  function rowToDraftValue(row) {
    return {
      no: String(row?.no ?? ''),
      left: String(row?.left ?? ''),
      w: row?.tuple?.[0] ?? '',
      x: row?.tuple?.[1] ?? '',
      y: row?.tuple?.[2] ?? '',
      z: row?.tuple?.[3] ?? '',
      candidates: Array.isArray(row?.candidates) ? row.candidates.slice(0, 8) : [],
    };
  }

  function setInputDraftRows(nextRows) {
    inputDraftRows = Array.isArray(nextRows)
      ? nextRows.map(row => ({
          no: String(row.no ?? '').trim(),
          left: String(row.left ?? '').trim(),
          tuple: Array.isArray(row.tuple) ? row.tuple.slice(0, 4) : [null, null, null, null],
          candidates: Array.isArray(row.candidates) ? row.candidates.map(tuple => tuple.slice(0, 4)) : [],
        }))
      : [];
    inputDraftEditIndex = -1;
    window.PATTERNIX_INPUT_DRAFT_ROWS = inputDraftRows;
    if (typeof renderInputDraftRows === 'function') {
      renderInputDraftRows();
    }
    return inputDraftRows.slice();
  }

  function getInputDraftRows() {
    return inputDraftRows.slice();
  }

  function serializeDraftRows(rowsList) {
    return rowsList.map(row => {
      const tuple = (row.tuple || [null, null, null, null])
        .map(value => value === null || value === undefined || value === '' ? '?' : value)
        .join(',');
      const candidates = Array.isArray(row.candidates)
        ? row.candidates.map(candidate => `|${candidate.join(',')}`).join('')
        : '';
      return `${row.no},${row.left},${tuple}${candidates}`;
    }).join('\n');
  }

  function readInputDraftRowFromForm() {
    return {
      no: String(document.getElementById('inputNo')?.value || '').trim(),
      left: String(document.getElementById('inputLeft')?.value || '').trim(),
      tuple: [
        parseNumericCell(document.getElementById('inputW')?.value),
        parseNumericCell(document.getElementById('inputX')?.value),
        parseNumericCell(document.getElementById('inputY')?.value),
        parseNumericCell(document.getElementById('inputZ')?.value),
      ],
      candidates: inputCandidateDraftRows.map(candidate => candidate.slice(0, 4)),
    };
  }

  function fillInputDraftForm(row) {
    const value = rowToDraftValue(row);
    const fields = [
      ['inputNo', value.no],
      ['inputLeft', value.left],
      ['inputW', value.w],
      ['inputX', value.x],
      ['inputY', value.y],
      ['inputZ', value.z],
    ];

    fields.forEach(([id, nextValue]) => {
      const el = document.getElementById(id);
      if (el) {
        el.value = nextValue;
      }
    });

    setInputCandidateDraftRows(value.candidates);
  }

  function clearInputDraftForm() {
    ['inputNo', 'inputLeft', 'inputW', 'inputX', 'inputY', 'inputZ']
      .forEach(id => {
        const el = document.getElementById(id);
        if (el) {
          el.value = '';
        }
      });
    inputDraftEditIndex = -1;
    const btn = document.getElementById('inputDraftActionBtn');
    if (btn) {
      btn.textContent = 'Add Row';
    }
    clearInputCandidateDraftRows();
  }

  function setInputCandidateDraftRows(nextRows) {
    inputCandidateDraftRows = Array.isArray(nextRows)
      ? nextRows.map(tuple => [
          parseNumericCell(tuple?.[0]),
          parseNumericCell(tuple?.[1]),
          parseNumericCell(tuple?.[2]),
          parseNumericCell(tuple?.[3]),
        ]).filter(tuple => tuple.every(value => value !== null))
      : [];
    inputCandidateEditIndex = -1;
    renderInputCandidateDraftRows();
    return inputCandidateDraftRows.slice();
  }

  function readInputCandidateRowFromForm() {
    return [
      parseNumericCell(document.getElementById('inputCandW')?.value),
      parseNumericCell(document.getElementById('inputCandX')?.value),
      parseNumericCell(document.getElementById('inputCandY')?.value),
      parseNumericCell(document.getElementById('inputCandZ')?.value),
    ];
  }

  function fillInputCandidateForm(tuple) {
    const fields = [
      ['inputCandW', tuple?.[0] ?? ''],
      ['inputCandX', tuple?.[1] ?? ''],
      ['inputCandY', tuple?.[2] ?? ''],
      ['inputCandZ', tuple?.[3] ?? ''],
    ];

    fields.forEach(([id, nextValue]) => {
      const el = document.getElementById(id);
      if (el) {
        el.value = nextValue;
      }
    });
  }

  function clearInputCandidateForm() {
    ['inputCandW', 'inputCandX', 'inputCandY', 'inputCandZ'].forEach(id => {
      const el = document.getElementById(id);
      if (el) {
        el.value = '';
      }
    });
    inputCandidateEditIndex = -1;
    const btn = document.getElementById('inputCandidateActionBtn');
    if (btn) {
      btn.textContent = 'Add Candidate';
    }
  }

  function renderInputCandidateDraftRows() {
    const host = document.getElementById('inputCandidateTable');
    const countPill = document.getElementById('inputCandidateCount');
    if (!host) {
      return [];
    }

    if (countPill) {
      countPill.textContent = `${inputCandidateDraftRows.length} candidate${inputCandidateDraftRows.length === 1 ? '' : 's'}`;
    }

    if (!inputCandidateDraftRows.length) {
      host.innerHTML = '<div class="no-data"><div class="no-data-icon">◈</div><div class="no-data-text">No candidates added yet.</div></div>';
      return [];
    }

    const rowsHtml = inputCandidateDraftRows.map((tuple, index) => `
      <tr${index === inputCandidateEditIndex ? ' style="background: rgba(0,113,227,0.06)"' : ''}>
        <td style="font-weight:600">${escapeHtml(candidateTupleToText(tuple))}</td>
        <td style="white-space:nowrap">
          <button class="btn btn-secondary btn-sm" onclick="editInputCandidateRow(${index})">Edit</button>
          <button class="btn btn-secondary btn-sm" onclick="removeInputCandidateRow(${index})">Remove</button>
        </td>
      </tr>
    `).join('');

    host.innerHTML = `
      <table>
        <thead>
          <tr>
            <th>Candidate tuple</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>${rowsHtml}</tbody>
      </table>`;
    return inputCandidateDraftRows.slice();
  }

  function upsertInputCandidateRow() {
    const tuple = readInputCandidateRowFromForm();
    if (tuple.some(value => value === null)) {
      throw new Error('Candidate tuple needs 4 numbers.');
    }

    if (inputCandidateEditIndex >= 0 && inputCandidateEditIndex < inputCandidateDraftRows.length) {
      inputCandidateDraftRows[inputCandidateEditIndex] = tuple;
    } else {
      inputCandidateDraftRows.push(tuple);
    }

    inputCandidateEditIndex = -1;
    clearInputCandidateForm();
    renderInputCandidateDraftRows();
  }

  function removeInputCandidateRow(index) {
    if (index < 0 || index >= inputCandidateDraftRows.length) {
      return;
    }

    inputCandidateDraftRows.splice(index, 1);
    if (inputCandidateEditIndex === index) {
      clearInputCandidateForm();
    } else if (inputCandidateEditIndex > index) {
      inputCandidateEditIndex -= 1;
    }
    renderInputCandidateDraftRows();
  }

  function editInputCandidateRow(index) {
    if (index < 0 || index >= inputCandidateDraftRows.length) {
      return;
    }

    inputCandidateEditIndex = index;
    fillInputCandidateForm(inputCandidateDraftRows[index]);
    const btn = document.getElementById('inputCandidateActionBtn');
    if (btn) {
      btn.textContent = 'Update Candidate';
    }
    renderInputCandidateDraftRows();
  }

  function clearInputCandidateDraftRows() {
    inputCandidateDraftRows = [];
    inputCandidateEditIndex = -1;
    clearInputCandidateForm();
    renderInputCandidateDraftRows();
  }

  function bindEnterKeyHandlers() {
    const bind = (ids, handler) => {
      ids.forEach(id => {
        const el = document.getElementById(id);
        if (!el || el.dataset.enterBound === '1') {
          return;
        }

        el.dataset.enterBound = '1';
        el.addEventListener('keydown', event => {
          if (event.key === 'Enter') {
            event.preventDefault();
            handler();
          }
        });
      });
    };

    bind(['inputNo', 'inputLeft', 'inputW', 'inputX', 'inputY', 'inputZ'], () => {
      if (typeof addInputDraftRow === 'function') {
        addInputDraftRow();
      }
    });

    bind(['inputCandW', 'inputCandX', 'inputCandY', 'inputCandZ'], () => {
      if (typeof addInputCandidateRow === 'function') {
        addInputCandidateRow();
      }
    });
  }

  function renderInputDraftRows() {
    const host = document.getElementById('inputDraftTable');
    const countPill = document.getElementById('inputDraftCount');
    if (!host) {
      return [];
    }

    if (countPill) {
      countPill.textContent = `${inputDraftRows.length} row${inputDraftRows.length === 1 ? '' : 's'}`;
    }

    if (!inputDraftRows.length) {
      host.innerHTML = '<div class="no-data"><div class="no-data-icon">◈</div><div class="no-data-text">No draft rows yet.</div></div>';
      return [];
    }

    const rowsHtml = inputDraftRows.map((row, index) => {
      const tupleText = row.tuple.map(value => value === null || value === undefined ? '?' : value).join(', ');
      const candidateText = row.candidates.length ? row.candidates.map(tuple => candidateTupleToText(tuple)).join(' | ') : '—';
      return `
        <tr${index === inputDraftEditIndex ? ' style="background: rgba(0,113,227,0.06)"' : ''}>
          <td style="font-weight:600">${escapeHtml(row.no || '—')}</td>
          <td>${escapeHtml(row.left || '—')}</td>
          <td>${escapeHtml(tupleText)}</td>
          <td>${escapeHtml(candidateText)}</td>
          <td style="white-space:nowrap">
            <button class="btn btn-secondary btn-sm" onclick="editInputDraftRow(${index})">Edit</button>
            <button class="btn btn-secondary btn-sm" onclick="removeInputDraftRow(${index})">Remove</button>
          </td>
        </tr>`;
    }).join('');

    host.innerHTML = `
      <table>
        <thead>
          <tr>
            <th>No</th>
            <th>Left</th>
            <th>w, x, y, z</th>
            <th>Candidates</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>${rowsHtml}</tbody>
      </table>`;

    return inputDraftRows.slice();
  }

  function upsertInputDraftRow() {
    const row = readInputDraftRowFromForm();
    if (!row.no || !row.left) {
      throw new Error('No and Left are required.');
    }

    if (inputDraftEditIndex >= 0 && inputDraftEditIndex < inputDraftRows.length) {
      inputDraftRows[inputDraftEditIndex] = row;
    } else {
      inputDraftRows.push(row);
    }

    inputDraftEditIndex = -1;
    clearInputDraftForm();
    renderInputDraftRows();
  }

  function removeInputDraftRow(index) {
    if (index < 0 || index >= inputDraftRows.length) {
      return;
    }

    inputDraftRows.splice(index, 1);
    if (inputDraftEditIndex === index) {
      clearInputDraftForm();
    } else if (inputDraftEditIndex > index) {
      inputDraftEditIndex -= 1;
    }
    renderInputDraftRows();
  }

  function editInputDraftRow(index) {
    if (index < 0 || index >= inputDraftRows.length) {
      return;
    }

    inputDraftEditIndex = index;
    fillInputDraftForm(inputDraftRows[index]);
    const btn = document.getElementById('inputDraftActionBtn');
    if (btn) {
      btn.textContent = 'Update Row';
    }
    renderInputDraftRows();
  }

  function clearInputDraftRows() {
    setInputDraftRows([]);
    clearInputDraftForm();
  }

  function promptValue(label, current) {
    const value = prompt(label, current ?? '');
    if (value === null) {
      throw new Error('Edit cancelled.');
    }
    return value.trim();
  }

  function showError(message) {
    alert(message);
    if (typeof showMsg === 'function') {
      showMsg('parseResult', 'yellow', message);
    }
  }

  function setRowsFromApi(rowsPayload) {
    rows = rowsPayload.map((row, index) => normalizeRow(row, index));
    if (typeof renderDataset === 'function') {
      renderDataset();
    }
    if (typeof populateUnknownSel === 'function') {
      populateUnknownSel();
    }
    if (typeof updateProof === 'function') {
      updateProof();
    }
    if (typeof renderDatasetBrowser === 'function') {
      renderDatasetBrowser();
    }
  }

  async function listDatasets() {
    return apiRequest('/api/datasets');
  }

  function populateDatasetPicker(datasets) {
    const sel = document.getElementById('datasetSel');
    if (!sel) {
      return;
    }

    sel.innerHTML = '<option value="">— Select dataset —</option>';
    datasets.forEach(dataset => {
      const label = `${dataset.name} • ${dataset.totalRows} rows • ${new Date(dataset.updatedAt || dataset.createdAt).toLocaleString()}`;
      sel.innerHTML += `<option value="${dataset.id}">${label}</option>`;
    });

    const activeId = getActiveDatasetId();
    if (activeId) {
      sel.value = activeId;
    } else if (datasets[0]?.id) {
      sel.value = datasets[0].id;
    }
  }

  function formatDatasetDate(dataset) {
    const stamp = dataset?.updatedAt || dataset?.createdAt;
    if (!stamp) {
      return 'Unknown';
    }
    try {
      return new Date(stamp).toLocaleString();
    } catch (_) {
      return String(stamp);
    }
  }

  function escapeHtml(value) {
    return String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function getDatasetSearchTerm() {
    const input = document.getElementById('datasetSearchInput');
    return (input?.value || '').trim().toLowerCase();
  }

  function renderDatasetBrowser(datasets = datasetCache) {
    const host = document.getElementById('datasetBrowser');
    const countPill = document.getElementById('datasetBrowserCount');
    if (!host) {
      return [];
    }

    const term = getDatasetSearchTerm();
    const activeId = getActiveDatasetId();
    const filtered = (datasets || []).filter(dataset => {
      if (!term) {
        return true;
      }

      return [dataset.name, dataset.id, dataset.totalRows, dataset.knownRows, dataset.unknownRows]
        .some(value => String(value ?? '').toLowerCase().includes(term));
    });

    if (countPill) {
      countPill.textContent = `${filtered.length} dataset${filtered.length === 1 ? '' : 's'}`;
    }

    if (!filtered.length) {
      host.innerHTML = term
        ? '<div class="no-data"><div class="no-data-icon">⌕</div><div class="no-data-text">No datasets match your search.</div></div>'
        : '<div class="no-data"><div class="no-data-icon">◈</div><div class="no-data-text">No datasets saved in SQL yet.</div></div>';
      return filtered;
    }

    const rowsHtml = filtered.map(dataset => {
      const isActive = dataset.id === activeId;
      return `
        <tr${isActive ? ' style="background: rgba(0,113,227,0.06)"' : ''}>
          <td style="font-weight:600">${escapeHtml(dataset.name || 'Untitled Dataset')}${isActive ? ' <span class="pill pill-blue" style="margin-left:6px">Active</span>' : ''}</td>
          <td>${dataset.totalRows ?? 0}</td>
          <td>${dataset.knownRows ?? 0}</td>
          <td>${dataset.unknownRows ?? 0}</td>
          <td>${formatDatasetDate(dataset)}</td>
          <td style="white-space:nowrap">
            <button class="btn btn-secondary btn-sm" onclick="selectDatasetById('${dataset.id}')">Load</button>
            <button class="btn btn-secondary btn-sm" onclick="deleteDatasetById('${dataset.id}')">Delete</button>
          </td>
        </tr>`;
    }).join('');

    host.innerHTML = `
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Rows</th>
            <th>Known</th>
            <th>Unknown</th>
            <th>Updated</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>${rowsHtml}</tbody>
      </table>`;

    return filtered;
  }

  async function refreshDatasetBrowser() {
    const datasets = await listDatasets();
    setDatasetCache(datasets);
    populateDatasetPicker(datasets);
    renderDatasetBrowser(datasets);
    return datasets;
  }

  async function importDataset(rawInput) {
    const response = await apiRequest('/api/datasets', {
      method: 'POST',
      body: JSON.stringify({
        name: 'Patternix Dataset',
        rawInput,
      }),
    });

    setActiveDatasetId(response.id);
    setActiveDatasetName(response.name);
    const rowsPayload = await apiRequest(`/api/datasets/${response.id}/rows`);
    setRowsFromApi(rowsPayload);
    await refreshDatasetBrowser();
    return response;
  }

  async function refreshRows(datasetId) {
    if (!datasetId) {
      return;
    }
    const rowsPayload = await apiRequest(`/api/datasets/${datasetId}/rows`);
    setRowsFromApi(rowsPayload);
  }

  function clearCurrentDatasetState() {
    rows = [];
    theoryResults = [];
    candidatesGenerated = [];
    setActiveDatasetId('');
    setActiveDatasetName('');
    if (typeof renderDataset === 'function') {
      renderDataset();
    }
    if (typeof renderBacktest === 'function') {
      renderBacktest();
    }
    if (typeof populateUnknownSel === 'function') {
      populateUnknownSel();
    }
    if (typeof updateProof === 'function') {
      updateProof();
    }
  }

  async function updateDataset(datasetId, payload) {
    return apiRequest(`/api/datasets/${datasetId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  }

  async function updateRow(datasetId, rowId, payload) {
    const response = await apiRequest(`/api/datasets/${datasetId}/rows/${rowId}`, {
      method: 'PATCH',
      body: JSON.stringify(payload),
    });

    await refreshRows(datasetId);
    if (payload && typeof payload.name === 'string') {
      setActiveDatasetName(payload.name);
      await refreshDatasetBrowser();
    }
    return response;
  }

  async function deleteRow(datasetId, rowId) {
    return apiRequest(`/api/datasets/${datasetId}/rows/${rowId}`, {
      method: 'DELETE',
    });
  }

  async function runDataset(datasetId) {
    const response = await apiRequest(`/api/datasets/${datasetId}/run`, { method: 'POST' });
    theoryResults = (response.theoryResults || []).map(normalizeTheoryResult);
    candidatesGenerated = [];
    if (typeof renderBacktest === 'function') {
      renderBacktest();
    }
    if (typeof populateUnknownSel === 'function') {
      populateUnknownSel();
    }
    if (typeof updateProof === 'function') {
      updateProof();
    }
    return response;
  }

  async function solveDataset(datasetId, request, rowForRender) {
    const response = await apiRequest(`/api/datasets/${datasetId}/solve`, {
      method: 'POST',
      body: JSON.stringify(request),
    });

    theoryResults = (response.theoryResults || []).map(normalizeTheoryResult);
    candidatesGenerated = (response.candidates || []).map(normalizeCandidate);

    if (typeof renderCandidates === 'function') {
      renderCandidates(rowForRender || buildManualRowFromRequest(request), candidatesGenerated);
    }
    if (typeof updateProof === 'function') {
      updateProof(rowForRender || buildManualRowFromRequest(request), candidatesGenerated);
    }
    if (typeof switchTab === 'function') {
      switchTab('solver');
    }

    return response;
  }

  function getSelectedRowFromSelect() {
    const select = document.getElementById('unknownSel');
    if (!select) {
      return null;
    }

    const selected = select.value;
    if (!selected) {
      return null;
    }

    return rows.find(row => String(row.apiRowId || row.id || row.rowIndex) === String(selected)) || null;
  }

  async function restoreDatasetIfAvailable() {
    try {
      const datasets = await refreshDatasetBrowser();
      if (!datasets.length) {
        clearCurrentDatasetState();
        return;
      }

      const activeId = getActiveDatasetId();
      let dataset = null;
      if (activeId) {
        dataset = datasets.find(item => item.id === activeId) || null;
        if (!dataset) {
          try {
            dataset = await apiRequest(`/api/datasets/${activeId}`);
          } catch (_) {
            dataset = null;
          }
        }
      }
      if (!dataset) {
        dataset = datasets[0];
      }

      if (!dataset?.id) {
        clearCurrentDatasetState();
        return;
      }

      setActiveDatasetId(dataset.id);
      setActiveDatasetName(dataset.name);
      await refreshRows(dataset.id);
      if (typeof switchTab === 'function') {
        switchTab('dataset');
      }
    } catch (error) {
      console.warn('Patternix restore failed:', error);
      clearCurrentDatasetState();
    }
  }

  window.selectDatasetById = async function selectDatasetById(datasetId) {
    if (!datasetId) {
      return;
    }

    try {
      const dataset = await apiRequest(`/api/datasets/${datasetId}`);
      setActiveDatasetId(dataset.id);
      setActiveDatasetName(dataset.name);
      await refreshRows(dataset.id);
      await refreshDatasetBrowser();
      if (typeof switchTab === 'function') {
        switchTab('dataset');
      }
    } catch (error) {
      showError(error.message || 'Failed to load dataset.');
    }
  };

  window.deleteDatasetById = async function deleteDatasetById(datasetId) {
    if (!datasetId) {
      return;
    }

    const label = (datasetCache.find(item => item.id === datasetId)?.name) || 'this dataset';
    const confirmed = confirm(`Delete ${label}? This will remove the dataset and all saved rows/results from SQL.`);
    if (!confirmed) {
      return;
    }

    try {
      await apiRequest(`/api/datasets/${datasetId}`, { method: 'DELETE' });
      const datasets = await refreshDatasetBrowser();
      if (getActiveDatasetId() === datasetId) {
        const nextDataset = datasets.find(item => item.id !== datasetId);
        if (nextDataset) {
          await window.selectDatasetById(nextDataset.id);
        } else {
          clearCurrentDatasetState();
        }
      } else {
        renderDatasetBrowser(datasets);
      }
      if (typeof showMsg === 'function') {
        showMsg('parseResult', 'green', `Deleted ${label}.`);
      }
    } catch (error) {
      showError(error.message || 'Failed to delete dataset.');
    }
  };

  window.deleteRowById = async function deleteRowById(rowId) {
    const datasetId = getActiveDatasetId();
    if (!datasetId) {
      alert('No active dataset to edit.');
      return;
    }

    const row = rows.find(item => String(item.apiRowId || item.id) === String(rowId));
    if (!row) {
      alert('Row not found.');
      return;
    }

    if (row.isLocked) {
      alert(`Row ${row.no} is locked and cannot be deleted.`);
      return;
    }

    const label = `Row ${row.no}`;
    const confirmed = confirm(`Delete ${label}? This will remove the row from SQL and cannot be undone.`);
    if (!confirmed) {
      return;
    }

    try {
      await deleteRow(datasetId, row.apiRowId || row.id);
      await refreshRows(datasetId);
      await refreshDatasetBrowser();
      if (typeof showMsg === 'function') {
        showMsg('parseResult', 'green', `${label} deleted.`);
      }
    } catch (error) {
      showError(error.message || 'Failed to delete row.');
    }
  };

  window.PatternixApi = {
    getApiBaseUrl,
    setApiBaseUrl,
    getActiveDatasetId,
    setActiveDatasetId,
    listDatasets,
    importDataset,
    refreshRows,
    updateDataset,
    updateRow,
    deleteRow,
    runDataset,
    solveDataset,
    normalizeRow,
    normalizeTheoryResult,
    normalizeCandidate,
    populateDatasetPicker,
    refreshDatasetBrowser,
    renderDatasetBrowser,
    deleteDatasetById,
  };

  window.renameActiveDataset = async function renameActiveDataset() {
    const datasetId = getActiveDatasetId();
    if (!datasetId) {
      alert('No active dataset to rename.');
      return;
    }

    const currentName = (rows.length && document.getElementById('datasetTitleInput')?.value) || 'Patternix Dataset';
    const nextName = prompt('Dataset name', currentName);
    if (nextName === null) {
      return;
    }

    const trimmed = nextName.trim();
    if (!trimmed) {
      alert('Dataset name cannot be empty.');
      return;
    }

    await updateDataset(datasetId, { name: trimmed });
    const titleInput = document.getElementById('datasetTitleInput');
    if (titleInput) {
      titleInput.value = trimmed;
    }
    setActiveDatasetName(trimmed);
    await refreshDatasetBrowser();
    if (typeof showMsg === 'function') {
      showMsg('parseResult', 'green', `Dataset renamed to ${trimmed}.`);
    }
  };

  window.editRowById = async function editRowById(rowId) {
    const datasetId = getActiveDatasetId();
    if (!datasetId) {
      alert('No active dataset to edit.');
      return;
    }

    const row = rows.find(item => String(item.apiRowId || item.id) === String(rowId));
    if (!row) {
      alert('Row not found.');
      return;
    }

    if (!row.isCurrent) {
      alert(`Row ${row.no} is locked. Only the current unknown row can be edited.`);
      return;
    }

    try {
      const nextW = promptValue('w', row.tuple?.[0] ?? '?');
      const nextX = promptValue('x', row.tuple?.[1] ?? '?');
      const nextY = promptValue('y', row.tuple?.[2] ?? '?');
      const nextZ = promptValue('z', row.tuple?.[3] ?? '?');
      const nextCandidates = promptValue('Candidates as 4-number tuples separated by |, or leave blank', (row.candidates || []).map(tuple => tuple.join(',')).join(' | '));

      const tuple = [nextW, nextX, nextY, nextZ].map(value => (value === '?' || value === '') ? null : Number(value));
      const candidates = nextCandidates
        ? nextCandidates.split('|').map(segment => parseCsvTuple(segment.trim())).filter(tuple4 => tuple4.length === 4 && tuple4.every(value => value === null || Number.isFinite(value))).map(tuple4 => tuple4.map(value => value === null ? null : Number(value)))
        : (row.candidates || []);

      await updateRow(datasetId, row.apiRowId || row.id, {
        w: tuple[0],
        x: tuple[1],
        y: tuple[2],
        z: tuple[3],
        candidates,
      });

      if (typeof showMsg === 'function') {
        showMsg('parseResult', 'green', `Row ${row.no} updated.`);
      }
    } catch (error) {
      if (error.message !== 'Edit cancelled.') {
        alert(error.message || 'Failed to update row.');
      }
    }
  };

  window.parseInput = async function parseInput() {
    const raw = inputDraftRows.length
      ? serializeDraftRows(inputDraftRows).trim()
      : (document.getElementById('inputArea')?.value?.trim() || '');
    if (!raw) {
      showError('Nothing to parse.');
      return;
    }

    try {
      await importDataset(raw);
      if (typeof addLog === 'function') {
        addLog('Parse', `${rows.length} rows imported into backend`, 'input');
      }
      if (typeof showMsg === 'function') {
        showMsg('parseResult', 'green', `Imported ${rows.length} rows into SQL-backed dataset.`);
      }
      if (typeof switchTab === 'function') {
        switchTab('dataset');
      }
    } catch (error) {
      showError(error.message || 'Failed to import dataset.');
    }
  };

  window.addInputDraftRow = function addInputDraftRow() {
    try {
      upsertInputDraftRow();
    } catch (error) {
      showError(error.message || 'Failed to add row.');
    }
  };

  window.addInputCandidateRow = function addInputCandidateRow() {
    try {
      upsertInputCandidateRow();
    } catch (error) {
      showError(error.message || 'Failed to add candidate.');
    }
  };

  window.removeInputDraftRow = removeInputDraftRow;
  window.editInputDraftRow = editInputDraftRow;
  window.renderInputDraftRows = renderInputDraftRows;
  window.removeInputCandidateRow = removeInputCandidateRow;
  window.editInputCandidateRow = editInputCandidateRow;
  window.renderInputCandidateDraftRows = renderInputCandidateDraftRows;
  window.clearInputCandidateDraftRows = clearInputCandidateDraftRows;
  window.clearInputDraftRows = clearInputDraftRows;
  window.loadExampleInputRows = function loadExampleInputRows() {
    setInputDraftRows([
      { no: '1', left: '15', tuple: [3, 4, 2, 6], candidates: [[3, 4, 2, 7], [3, 5, 2, 6]] },
      { no: '2', left: '22', tuple: [5, 6, 3, 8], candidates: [[5, 6, 3, 9]] },
      { no: '3', left: '18', tuple: [4, 3, 5, 6], candidates: [[4, 3, 6, 5]] },
      { no: '4', left: '28', tuple: [6, 7, 4, 11], candidates: [[6, 7, 4, 10]] },
      { no: '5', left: '19', tuple: [4, 4, 5, 6], candidates: [[4, 5, 4, 6]] },
      { no: '6', left: '24', tuple: [5, 5, 6, 8], candidates: [] },
      { no: '7', left: '30', tuple: [null, null, null, null], candidates: [[6, 8, 5, 11], [7, 7, 5, 11]] },
    ]);
    fillInputDraftForm({ no: '', left: '', tuple: [null, null, null, null], candidates: [] });
  };

  window.runAllTheories = async function runAllTheories() {
    const datasetId = getActiveDatasetId();
    if (!datasetId) {
      alert('Parse rows first so the dataset can be saved to the backend.');
      return;
    }

    const known = rows.filter(r => !r.isUnknown);
    if (rows.length < 2) {
      alert('Need at least 2 rows.');
      return;
    }
    if (known.length < 2) {
      alert('Need at least 2 known rows.');
      return;
    }

    try {
      await runDataset(datasetId);
      if (Array.isArray(customFormulas) && customFormulas.length && typeof evalCustom === 'function') {
        const known = rows.filter(r => !r.isUnknown);
        const customResults = customFormulas.map(cf => evalCustom(cf, known));
        theoryResults = theoryResults.concat(customResults).sort((a, b) => b.coverageScore - a.coverageScore);
      }
      if (typeof addLog === 'function') {
        addLog('Run', `${theoryResults.length} theories on ${known.length} rows`, 'theory');
      }

      if (typeof renderBacktest === 'function') {
        renderBacktest();
      }
      if (typeof populateUnknownSel === 'function') {
        populateUnknownSel();
      }
      if (typeof updateProof === 'function') {
        updateProof();
      }
      if (typeof switchTab === 'function') {
        switchTab('backtest');
      }
    } catch (error) {
      showError(error.message || 'Failed to run theories.');
    }
  };

  window.populateUnknownSel = function populateUnknownSel() {
    const sel = document.getElementById('unknownSel');
    if (!sel) {
      return;
    }

    sel.innerHTML = '<option value="">— Select unknown row —</option>';
    rows.filter(r => r.isCurrent).forEach(r => {
      const value = String(r.apiRowId || r.id || r.rowIndex);
      sel.innerHTML += `<option value="${value}">Row ${r.no} — Left=${r.left}</option>`;
    });
  };

  window.solveSelected = async function solveSelected() {
    const row = getSelectedRowFromSelect();
    if (!row) {
      alert('Select an unknown row.');
      return;
    }

    await window.generateCandidates(row);
  };

  window.solveManual = async function solveManual() {
    const row = buildManualRowFromRequest({
      rowNo: Number(document.getElementById('mNo')?.value || 0) || null,
      leftValue: Number(document.getElementById('mLeft')?.value || 0) || 0,
      w: document.getElementById('mW')?.value === '' || document.getElementById('mW')?.value === '?' ? null : Number(document.getElementById('mW')?.value),
      x: document.getElementById('mX')?.value === '' || document.getElementById('mX')?.value === '?' ? null : Number(document.getElementById('mX')?.value),
      y: document.getElementById('mY')?.value === '' || document.getElementById('mY')?.value === '?' ? null : Number(document.getElementById('mY')?.value),
      z: document.getElementById('mZ')?.value === '' || document.getElementById('mZ')?.value === '?' ? null : Number(document.getElementById('mZ')?.value),
      candidates: [],
    });

    await window.generateCandidates(row);
  };

  window.generateCandidates = async function generateCandidates(row) {
    const datasetId = getActiveDatasetId();
    if (!datasetId) {
      alert('Parse rows first so the dataset can be saved to the backend.');
      return;
    }

    const targetRow = row || getSelectedRowFromSelect();
    if (!targetRow) {
      alert('Select an unknown row.');
      return;
    }

    if (!targetRow.isCurrent) {
      alert(`Row ${targetRow.no} is locked. Only the current unknown row can be solved right now.`);
      return;
    }

    const request = targetRow.apiRowId || targetRow.id
      ? {
          rowId: targetRow.apiRowId || targetRow.id,
        }
      : {
          rowNo: Number(targetRow.no) || null,
          leftValue: Number(targetRow.left) || 0,
          w: targetRow.tuple?.[0] ?? null,
          x: targetRow.tuple?.[1] ?? null,
          y: targetRow.tuple?.[2] ?? null,
          z: targetRow.tuple?.[3] ?? null,
          candidates: targetRow.candidates || [],
        };

    try {
      await solveDataset(datasetId, request, targetRow);
      if (typeof addLog === 'function') {
        addLog('Solve', `${candidatesGenerated.length} candidates for Row ${targetRow.no}`, 'solve');
      }
    } catch (error) {
      showError(error.message || 'Failed to generate candidates.');
    }
  };
  window.setPatternixApiBaseUrl = setApiBaseUrl;
  window.getPatternixApiBaseUrl = getApiBaseUrl;
  window.escapeHtml = window.escapeHtml || escapeHtml;
  bindEnterKeyHandlers();

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      restoreDatasetIfAvailable().catch(error => console.warn(error));
    });
  } else {
    restoreDatasetIfAvailable().catch(error => console.warn(error));
  }
})();
