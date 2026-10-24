import { useCallback, useEffect, useMemo, useState } from 'react'
import './App.css'

type Device = {
  id: string
  deviceName: string
  hostname: string
  operatingSystem: string
  ipAddress: string
  status: string
  createdAtUtc?: string | null
  lastSeenAtUtc?: string | null
}

function normalizeDevices(payload: unknown): Device[] {
  if (Array.isArray(payload)) {
    return payload as Device[]
  }

  if (payload && typeof payload === 'object') {
    const data = payload as Record<string, unknown>

    for (const key of ['devices', 'items', 'data', '$values']) {
      if (Array.isArray(data[key])) {
        return data[key] as Device[]
      }
    }

    if (typeof data.id === 'string' || typeof data.hostname === 'string') {
      return [payload as Device]
    }
  }

  return []
}

function formatLastSeen(value?: string | null) {
  if (!value) return 'Never'

  const date = new Date(value)

  if (Number.isNaN(date.getTime())) return 'Unknown'

  return date.toLocaleString()
}

function App() {
  const [devices, setDevices] = useState<Device[]>([])
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('All')
  const [activePage, setActivePage] = useState('Overview')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)

  const loadDevices = useCallback(async () => {
    setLoading(true)
    setError('')

    try {
      const response = await fetch('/api/devices', {
        headers: { Accept: 'application/json' },
      })

      if (!response.ok) {
        if (response.status === 401 || response.status === 403) {
          throw new Error('API authentication failed. Check the development proxy configuration.')
        }

        throw new Error(`Devices API returned HTTP ${response.status}.`)
      }

      const payload: unknown = await response.json()
      setDevices(normalizeDevices(payload))
      setLastUpdated(new Date())
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Unable to load devices from the API.',
      )
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadDevices()
  }, [loadDevices])

  const online = devices.filter(
    device => device.status?.toLowerCase() === 'online',
  ).length

  const offline = devices.filter(
    device => device.status?.toLowerCase() === 'offline',
  ).length

  const other = devices.length - online - offline

  const filtered = useMemo(() => {
    return devices.filter(device => {
      const value = [
        device.deviceName,
        device.hostname,
        device.operatingSystem,
        device.ipAddress,
      ].join(' ').toLowerCase()

      const matchesSearch = value.includes(search.trim().toLowerCase())
      const state = device.status?.toLowerCase()

      const matchesStatus =
        statusFilter === 'All' ||
        (statusFilter === 'Online' && state === 'online') ||
        (statusFilter === 'Offline' && state === 'offline') ||
        (statusFilter === 'Other' && state !== 'online' && state !== 'offline')

      return matchesSearch && matchesStatus
    })
  }, [devices, search, statusFilter])

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-logo">R</div>
          <div>
            <strong>RemotePro</strong>
            <small>IT OPERATIONS</small>
          </div>
        </div>

        <p className="nav-heading">WORKSPACE</p>
        <div className="workspace">▦ &nbsp; My Organization &nbsp;⌄</div>

        <p className="nav-heading">MANAGEMENT</p>

        {[
          ['Overview', '◫'],
          ['Devices', '▣'],
          ['Alerts', '⚠'],
          ['Activity', '↗'],
          ['Settings', '⚙'],
        ].map(([name, icon]) => (
          <button
            key={name}
            className={`nav-item ${activePage === name ? 'active' : ''}`}
            onClick={() => setActivePage(name)}
          >
            <span>{icon}</span>{name}
          </button>
        ))}

        <div className="sidebar-footer">
          <div className="help-card">
            <strong>Need a hand?</strong>
            <p>Manage your IT operations from one place.</p>
          </div>
          <div className="user-profile">
            <div className="avatar">AD</div>
            <div>
              <strong>Administrator</strong>
              <small>IT Administrator</small>
            </div>
          </div>
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <div>Workspace <span>/</span> <strong>{activePage}</strong></div>
          <div className="environment"><span /> LOCAL ENVIRONMENT</div>
        </header>

        <div className="content">
          <section className="welcome">
            <div>
              <p className="eyebrow">IT OPERATIONS CENTER</p>
              <h1>{activePage === 'Overview' ? 'Infrastructure overview' : activePage}</h1>
              <p className="muted">Monitor your devices and IT infrastructure in one place.</p>
            </div>
            <button
              className="primary-button"
              onClick={() => void loadDevices()}
              disabled={loading}
            >
              {loading ? '↻ Loading...' : '↻ Refresh'}
            </button>
          </section>

          <section className="notice">
            <span className="notice-dot" />
            <div>
              <strong>
                {error
                  ? 'Device connection needs attention'
                  : loading
                    ? 'Connecting to RemotePro API'
                    : 'Live API integration active'}
              </strong>
              <p>
                {error
                  ? error
                  : loading
                    ? 'Retrieving registered devices...'
                    : lastUpdated
                      ? `Device data updated at ${lastUpdated.toLocaleTimeString()}.`
                      : 'Waiting for device data.'}
              </p>
            </div>
            <span className="setup-badge">{error ? 'ERROR' : loading ? 'LOADING' : 'LIVE'}</span>
          </section>

          <section className="stats-grid">
            <article className="stat-card">
              <span className="stat-label">Total devices</span>
              <strong className="stat-number">{devices.length}</strong>
              <span className="stat-note">Registered endpoints</span>
              <span className="stat-symbol blue">▣</span>
            </article>

            <article className="stat-card">
              <span className="stat-label">Online devices</span>
              <strong className="stat-number">{online}</strong>
              <span className="stat-note">Currently online</span>
              <span className="stat-symbol green">●</span>
            </article>

            <article className="stat-card">
              <span className="stat-label">Offline devices</span>
              <strong className="stat-number">{offline}</strong>
              <span className="stat-note">Need attention</span>
              <span className="stat-symbol red">○</span>
            </article>

            <article className="stat-card">
              <span className="stat-label">Other status</span>
              <strong className="stat-number">{other}</strong>
              <span className="stat-note">Unknown or pending</span>
              <span className="stat-symbol purple">◇</span>
            </article>
          </section>

          <section className="devices-panel">
            <div className="panel-heading">
              <div>
                <h2>Managed devices</h2>
                <p className="muted">Search and monitor registered endpoints.</p>
              </div>
              <span className="device-count">{devices.length} devices</span>
            </div>

            <div className="toolbar">
              <input
                aria-label="Search devices"
                placeholder="Search hostname, operating system or IP..."
                value={search}
                onChange={event => setSearch(event.target.value)}
              />

              <select
                aria-label="Filter device status"
                value={statusFilter}
                onChange={event => setStatusFilter(event.target.value)}
              >
                <option value="All">All statuses</option>
                <option value="Online">Online</option>
                <option value="Offline">Offline</option>
                <option value="Other">Other statuses</option>
              </select>
            </div>

            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>DEVICE</th>
                    <th>OPERATING SYSTEM</th>
                    <th>IP ADDRESS</th>
                    <th>STATUS</th>
                    <th>LAST SEEN</th>
                  </tr>
                </thead>

                <tbody>
                  {filtered.map((device, index) => {
                    const isOnline = device.status?.toLowerCase() === 'online'

                    return (
                      <tr key={device.id || device.hostname || index}>
                        <td>
                          <strong>{device.deviceName || device.hostname || 'Unnamed device'}</strong>
                          <small>{device.hostname || 'Hostname unavailable'}</small>
                        </td>
                        <td>{device.operatingSystem || '—'}</td>
                        <td>{device.ipAddress || '—'}</td>
                        <td>
                          <span className={`status ${isOnline ? 'status-online' : 'status-offline'}`}>
                            {device.status || 'Unknown'}
                          </span>
                        </td>
                        <td>{formatLastSeen(device.lastSeenAtUtc)}</td>
                      </tr>
                    )
                  })}

                  {filtered.length === 0 && (
                    <tr>
                      <td colSpan={5}>
                        <div className="empty-state">
                          <div className="empty-icon">▤</div>
                          <strong>
                            {loading
                              ? 'Loading devices...'
                              : error
                                ? 'Unable to load device data'
                                : devices.length === 0
                                  ? 'No registered devices found'
                                  : 'No matching devices'}
                          </strong>
                          <p>
                            {error
                              ? 'Check that the API and development proxy are running, then refresh.'
                              : loading
                                ? 'Contacting the RemotePro API.'
                                : devices.length === 0
                                  ? 'Registered endpoints will appear here when available.'
                                  : 'Try another search term or status filter.'}
                          </p>
                          {!loading && (
                            <button className="primary-button" onClick={() => void loadDevices()}>
                              Try again
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="table-footer">
              Showing {filtered.length} of {devices.length} devices
              <span>RemotePro · Local workspace</span>
            </div>
          </section>

          <footer className="footer">
            © 2026 RemotePro <span>IT Operations Dashboard</span>
          </footer>
        </div>
      </main>
    </div>
  )
}

export default App
