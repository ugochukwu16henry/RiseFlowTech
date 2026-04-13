const fs = require('fs');
const filePath = 'c:/Users/Dell/Documents/RiseFlowTech/src/RiseFlow.Web/src/pages/SchoolFeesPage.jsx';
let c = fs.readFileSync(filePath, 'utf8');
const hasCRLF = c.includes('\r\n');
if (hasCRLF) c = c.replace(/\r\n/g, '\n');

const old4 = "    </PageLayout>\n  );\n}";

const rosterSection = [
  '',
  '      {/* ── Student Roster tab ──────────────────────────────────────────── */}',
  "      {tab === 'roster' && (",
  '        <div>',
  '          <div style={{ display: \'flex\', gap: \'1rem\', flexWrap: \'wrap\', marginBottom: \'1rem\', alignItems: \'flex-end\' }}>',
  '            <div>',
  '              <label className="form-label">Select fee schedule</label>',
  '              <select className="form-input" style={{ minWidth: 280 }} value={rosterScheduleId}',
  '                onChange={(e) => setRosterScheduleId(e.target.value)}>',
  '                <option value="">— choose a schedule —</option>',
  '                {schedules.map(s => (',
  '                  <option key={s.id} value={s.id}>',
  '                    {s.termLabel} {s.academicYear}{s.gradeName ? \` \u2014 ${s.gradeName}\` : \'\'}{s.className ? \` / ${s.className}\` : \'\'}',
  '                  </option>',
  '                ))}',
  '              </select>',
  '            </div>',
  '            <button className="btn-primary-action btn-primary-action--ghost"',
  '              onClick={() => loadRoster(rosterScheduleId)} disabled={!rosterScheduleId}>',
  '              Refresh',
  '            </button>',
  '          </div>',
  '          {!rosterScheduleId && <p className="empty-state">Select a fee schedule above to see the student payment roster.</p>}',
  '          {loadingRoster && <p className="empty-state" aria-busy="true">Loading roster\u2026</p>}',
  '          {!loadingRoster && rosterScheduleId && roster.length === 0 && (',
  '            <p className="empty-state">No students found for this schedule.</p>',
  '          )}',
  '          {!loadingRoster && roster.length > 0 && (',
  '            <div className="table-scroll">',
  '              <table className="data-table">',
  '                <thead>',
  '                  <tr>',
  '                    <th>Student</th><th>Adm. No.</th><th>Grade</th><th>Class</th><th>Payment status</th><th>Confirmed on</th>',
  '                  </tr>',
  '                </thead>',
  '                <tbody>',
  '                  {roster.map(r => (',
  '                    <tr key={r.studentId}>',
  '                      <td>{r.studentName}</td>',
  '                      <td>{r.admissionNumber || \'\u2014\'}</td>',
  '                      <td>{r.gradeName || \'\u2014\'}</td>',
  '                      <td>{r.className || \'\u2014\'}</td>',
  '                      <td>',
  '                        <span className={`badge ${STATUS_CLASS[r.paymentStatus] || \'badge--neutral\'}`}>',
  '                          {STATUS_LABELS[r.paymentStatus] || r.paymentStatus}',
  '                        </span>',
  '                      </td>',
  '                      <td>{r.confirmedAtUtc ? new Date(r.confirmedAtUtc).toLocaleDateString() : \'\u2014\'}</td>',
  '                    </tr>',
  '                  ))}',
  '                </tbody>',
  '              </table>',
  '            </div>',
  '          )}',
  '        </div>',
  '      )}',
  '    </PageLayout>',
  '  );',
  '}',
].join('\n');

if (c.includes(old4)) {
  c = c.replace(old4, rosterSection);
  console.log('Roster section added. Verify:', c.includes("tab === 'roster' && ("));
} else {
  console.log('TARGET NOT FOUND. Last 200:', JSON.stringify(c.slice(-200)));
}

if (hasCRLF) c = c.replace(/\n/g, '\r\n');
fs.writeFileSync(filePath, c, 'utf8');
console.log('File written. Length:', c.length);
