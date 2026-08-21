import { Component, type ErrorInfo, type ReactNode } from 'react'
import { config } from '../config'

interface Props {
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * Catches render-time exceptions and shows a recoverable screen instead of an
 * empty page.
 *
 * Without this, one thrown error in any component unmounts the whole tree and
 * leaves a white rectangle — the worst possible failure mode for a link someone
 * was invited to click. Reviewers do not open the console.
 *
 * Class syntax is not a style choice: `componentDidCatch` has no hooks
 * equivalent, so an error boundary has to be a class component.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Console only. There is deliberately no third-party error reporter here:
    // adding one would ship a visitor's session to a vendor to collect
    // information a personal planning tool has no use for.
    console.error('Unhandled render error', error, info.componentStack)
  }

  render() {
    const { error } = this.state
    if (!error) return this.props.children

    return (
      <div className="app-main">
        <div className="card" style={{ padding: 24 }}>
          <h1>Something went wrong.</h1>
          <p className="hint">
            This page hit an error it could not recover from. Reloading usually clears it.
          </p>
          <div className="form-actions">
            <button className="btn btn-primary" onClick={() => window.location.reload()}>
              Reload
            </button>
            <a className="btn" href={config.basePath}>
              Back to the dashboard
            </a>
          </div>

          {/*
            The message is shown in development only. In a deployed build it stays
            in the console: a visitor cannot act on a stack trace, and an internal
            error string is not something to print onto a public page.
          */}
          {import.meta.env.DEV && (
            <pre style={{ marginTop: 16, whiteSpace: 'pre-wrap', fontSize: 12 }}>{error.message}</pre>
          )}
        </div>
      </div>
    )
  }
}
