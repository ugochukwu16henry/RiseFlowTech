import { useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageLayout from '../components/PageLayout';
import { apiFetch, getApiBase } from '../api';
import './ExcelImportPage.css';

export default function ExcelImportPage() {
  const [file, setFile] = useState(null);
  const [preview, setPreview] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState(null);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState(null);
  const [dragOver, setDragOver] = useState(false);

  const loadPreview = useCallback(async (fileObj) => {
    if (!fileObj || !fileObj.name?.toLowerCase().endsWith('.xlsx')) {
      setPreviewError('Please select an .xlsx file.');
      setPreview(null);
      return;
    }
    setPreviewError(null);
    setPreviewLoading(true);
    setPreview(null);
    const form = new FormData();
    form.append('file', fileObj);
    try {
      const res = await apiFetch('/api/students/bulk-upload-preview?previewRows=5', {
        method: 'POST',
        body: form,
      });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || 'Preview failed');
      }
      const data = await res.json();
      setPreview(data);
      setFile(fileObj);
    } catch (e) {
      setPreviewError(e.message || 'Preview failed');
      setPreview(null);
    } finally {
      setPreviewLoading(false);
    }
  }, []);

  const handleFileSelect = (e) => {
    const f = e.target.files?.[0];
    if (f) loadPreview(f);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    setDragOver(false);
    const f = e.dataTransfer?.files?.[0];
    if (f) loadPreview(f);
  };

  const handleDragOver = (e) => {
    e.preventDefault();
    setDragOver(true);
  };

  const handleDragLeave = () => setDragOver(false);

  const handleImport = async () => {
    if (!file || !preview) return;
    setImporting(true);
    setImportResult(null);
    const form = new FormData();
    form.append('file', file);
    try {
      const res = await apiFetch('/api/students/bulk-upload', { method: 'POST', body: form });
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      setImportResult(data);
      setFile(null);
      setPreview(null);
    } catch (e) {
      setImportResult({ error: e.message });
    } finally {
      setImporting(false);
    }
  };

  const downloadErrorRows = () => {
    if (!importResult?.errorRows?.length) return;
    const headers = 'Row,FirstName,LastName,Errors\n';
    const rows = importResult.errorRows.map((r) =>
      `${r.rowIndex},"${(r.firstName || '').replace(/"/g, '""')}","${(r.lastName || '').replace(/"/g, '""')}","${(r.errors || '').replace(/"/g, '""')}"`
    ).join('\n');
    const blob = new Blob([headers + rows], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'RiseFlow-import-errors.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  };

  const hasValidationErrors = preview?.validationErrors?.length > 0;
  const duplicateWarningCount = preview?.duplicateWarnings?.length ?? 0;

  return (
    <PageLayout title="Import students (Excel)">
      <div className="excel-import">
        <section className="excel-section">
          <h2 className="section-title">1. Download template</h2>
          <p className="card-desc">Use the template aligned with African ministry requirements (NIN, Class, Parent, etc.).</p>
          <a
            href={`${getApiBase()}/api/students/bulk-upload-template`}
            target="_blank"
            rel="noopener noreferrer"
            className="btn-excel btn-download"
          >
            Download sample Excel
          </a>
        </section>

        <section className="excel-section">
          <h2 className="section-title">2. Upload & preview</h2>
          <div
            className={`excel-dropzone ${dragOver ? 'excel-dropzone--active' : ''}`}
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
          >
            <input
              type="file"
              accept=".xlsx"
              onChange={handleFileSelect}
              className="excel-input"
              aria-label="Choose Excel file"
            />
            {previewLoading ? (
              <div className="excel-progress">
                <div className="excel-progress-bar" role="progressbar" aria-valuetext="Loading preview" />
                <span>Reading file…</span>
              </div>
            ) : (
              <p className="excel-dropzone-text">
                Drag and drop your .xlsx file here, or <span className="excel-dropzone-browse">browse</span>
              </p>
            )}
          </div>
          {previewError && <p className="excel-error">{previewError}</p>}

          {preview && preview.previewRows?.length > 0 && (
            <>
              <p className="card-desc" style={{ marginTop: '1rem' }}>
                First 5 rows below. {hasValidationErrors ? <strong className="text-red">Rows with errors are highlighted — fix before importing.</strong> : 'Data looks good.'}
                {duplicateWarningCount > 0 && (
                  <span className="excel-duplicate-note"> {duplicateWarningCount} row(s) are duplicates (already in school or repeated in file) and will be skipped on import.</span>
                )}
              </p>
              <div className="data-table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Row</th>
                      <th>FirstName</th>
                      <th>LastName</th>
                      <th>Gender</th>
                      <th>DateOfBirth</th>
                      <th>NIN</th>
                      <th>Class</th>
                      <th>ParentName</th>
                      <th>ParentPhone</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.previewRows.map((row) => (
                      <tr key={row.rowIndex} className={row.hasErrors ? 'row-error' : ''}>
                        <td>{row.rowIndex}</td>
                        <td>{row.firstName}</td>
                        <td>{row.lastName}</td>
                        <td>{row.gender ?? '—'}</td>
                        <td>{row.dateOfBirth ?? '—'}</td>
                        <td>{row.nIN ?? '—'}</td>
                        <td>{row.className ?? '—'}</td>
                        <td>{row.parentName ?? '—'}</td>
                        <td>{row.parentPhone ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="card-desc">Total rows in file: {preview.totalRows}</p>
              <button
                type="button"
                className="btn-excel btn-import"
                onClick={handleImport}
                disabled={importing || hasValidationErrors}
              >
                {importing ? 'Importing…' : 'Confirm and import'}
              </button>
              {hasValidationErrors && (
                <p className="excel-hint">Fix validation errors (e.g. missing FirstName/LastName or unknown Class) in your file and upload again.</p>
              )}
            </>
          )}
        </section>

        {importResult && (
          <div className="excel-modal" role="dialog" aria-labelledby="import-result-title">
            <div className="excel-modal-content">
              <h3 id="import-result-title" className="excel-modal-title">
                {importResult.error ? 'Import failed' : 'Import complete'}
              </h3>
              {importResult.error ? (
                <p className="excel-error">{importResult.error}</p>
              ) : (
                <>
                  <p className="excel-success-msg">{importResult.billingMessage}</p>
                  {importResult.importedCount != null && (
                    <p className="card-desc">
                      Imported: {importResult.importedCount} new student(s). Total students: {importResult.totalStudentsAfter}.
                      {importResult.skippedDuplicateCount > 0 && (
                        <span> {importResult.skippedDuplicateCount} row(s) skipped (duplicates).</span>
                      )}
                    </p>
                  )}
                  {importResult.errorRows?.length > 0 && (
                    <p className="card-desc">
                      <button type="button" className="btn-excel btn-link" onClick={downloadErrorRows}>
                            Download {importResult.errorRows.length} row(s) with errors
                          </button>
                      {' '}to fix and re-upload.
                    </p>
                  )}
                </>
              )}
              <div className="excel-modal-actions">
                <Link to="/school" className="btn-excel btn-secondary">Back to School Admin</Link>
                <button type="button" className="btn-excel btn-primary" onClick={() => setImportResult(null)}>Close</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </PageLayout>
  );
}
