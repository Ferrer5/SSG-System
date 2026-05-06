/**
 * SSG Auth Guard - Client-side authentication protection for dashboard pages
 * 
 * This script provides:
 * 1. Immediate auth check before page content renders (prevents flash of protected content)
 * 2. Handles bfcache (back-forward cache) in Safari/iOS and Firefox
 * 3. Periodic auth verification for long-running sessions
 * 4. Mobile swipe-back detection and handling
 * 
 * Usage: Include this script in the <head> of all protected dashboard pages:
 * <script src="~/js/auth-guard.js"></script>
 */

(function() {
    'use strict';

    const AUTH_CHECK_INTERVAL = 30000; // 30 seconds
    const AUTH_ENDPOINT = '/Home/CheckAuth';
    const LOGIN_URL = '/Home/Index';
    
    // Check if we're already on the login page to avoid redirect loops
    const currentPath = window.location.pathname.toLowerCase();
    const isLoginPage = currentPath === '/' || 
                        currentPath === '/home' || 
                        currentPath === '/home/index' || 
                        currentPath === '/home/login';
    
    if (isLoginPage) {
        return; // Don't run auth checks on login page
    }

    /**
     * Immediate auth check - runs synchronously before DOM is ready
     * Uses sessionStorage for quick local check, then validates with server
     */
    function immediateAuthCheck() {
        // Quick local check using sessionStorage flag
        const lastAuthCheck = sessionStorage.getItem('ssg_last_auth_check');
        const lastAuthResult = sessionStorage.getItem('ssg_last_auth_result');
        const now = Date.now();
        
        // If we recently confirmed auth (within 5 seconds), trust it
        if (lastAuthCheck && lastAuthResult === 'true' && (now - parseInt(lastAuthCheck)) < 5000) {
            return Promise.resolve(true);
        }
        
        return validateAuthWithServer();
    }

    /**
     * Server-side auth validation
     */
    function validateAuthWithServer() {
        // Add cache-buster to prevent cached responses
        const url = AUTH_ENDPOINT + '?_=' + Date.now();
        
        return fetch(url, {
            method: 'GET',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('Auth check failed');
            }
            return response.json();
        })
        .then(data => {
            // Store result in sessionStorage for quick access
            sessionStorage.setItem('ssg_last_auth_check', Date.now().toString());
            sessionStorage.setItem('ssg_last_auth_result', data.authenticated ? 'true' : 'false');
            
            if (!data.authenticated) {
                redirectToLogin('server_denied');
                return false;
            }
            return true;
        })
        .catch(error => {
            console.error('Auth validation error:', error);
            // On error, assume not authenticated for security
            redirectToLogin('error');
            return false;
        });
    }

    /**
     * Redirect to login page with appropriate parameters
     */
    function redirectToLogin(reason) {
        // Clear local auth flags
        sessionStorage.removeItem('ssg_last_auth_check');
        sessionStorage.removeItem('ssg_last_auth_result');
        
        // Build redirect URL with reason for debugging
        const separator = LOGIN_URL.includes('?') ? '&' : '?';
        const redirectUrl = LOGIN_URL + separator + 'auth=required&reason=' + reason + '&returnUrl=' + encodeURIComponent(window.location.pathname);
        
        // Replace current history entry so back button won't return to this protected page
        if (window.history.replaceState) {
            window.history.replaceState(null, '', redirectUrl);
        }
        
        window.location.replace(redirectUrl);
    }

    /**
     * Handle pageshow event (fires on back button navigation and bfcache restore)
     * This is critical for iOS Safari and Firefox which use bfcache
     */
    function handlePageShow(event) {
        // event.persisted is true when page is loaded from bfcache
        if (event.persisted) {
            console.log('Page restored from bfcache, revalidating auth...');
            // Force immediate server check when restored from cache
            sessionStorage.removeItem('ssg_last_auth_check');
            sessionStorage.removeItem('ssg_last_auth_result');
        }
        
        // Always validate auth on pageshow
        validateAuthWithServer();
    }

    /**
     * Handle visibility change (tab becomes active again)
     */
    function handleVisibilityChange() {
        if (!document.hidden) {
            // Tab became visible - check auth in case session expired while away
            const lastCheck = sessionStorage.getItem('ssg_last_auth_check');
            const now = Date.now();
            
            // Only check if we haven't checked recently (within 10 seconds)
            if (!lastCheck || (now - parseInt(lastCheck)) > 10000) {
                validateAuthWithServer();
            }
        }
    }

    /**
     * Set up periodic auth checks for long-running sessions
     */
    function setupPeriodicChecks() {
        setInterval(() => {
            // Only check if tab is visible (don't waste resources on background tabs)
            if (!document.hidden) {
                validateAuthWithServer();
            }
        }, AUTH_CHECK_INTERVAL);
    }

    /**
     * Prevent caching via meta tag injection (backup for server headers)
     */
    function injectNoCacheMetaTags() {
        const existingCacheControl = document.querySelector('meta[http-equiv="Cache-Control"]');
        if (!existingCacheControl) {
            const meta = document.createElement('meta');
            meta.setAttribute('http-equiv', 'Cache-Control');
            meta.setAttribute('content', 'no-cache, no-store, must-revalidate, private, max-age=0');
            document.head.insertBefore(meta, document.head.firstChild);
        }
    }

    /**
     * Detect mobile swipe-back gesture attempts
     * Uses touch event monitoring to detect edge swipes
     */
    function detectMobileSwipeBack() {
        let touchStartX = 0;
        const SWIPE_THRESHOLD = 50; // pixels from left edge
        
        document.addEventListener('touchstart', function(e) {
            touchStartX = e.touches[0].clientX;
        }, { passive: true });
        
        document.addEventListener('touchmove', function(e) {
            // If touch is near left edge and moving right, user might be trying to swipe back
            if (touchStartX < SWIPE_THRESHOLD) {
                const touchX = e.touches[0].clientX;
                if (touchX > touchStartX + 30) {
                    // Potential swipe back - preemptively validate auth
                    validateAuthWithServer();
                }
            }
        }, { passive: true });
    }

    // ==================== INITIALIZATION ====================

    // 1. Run immediate auth check (before DOM is ready)
    // This prevents any flash of protected content
    if (document.readyState === 'loading') {
        // Document still loading, run check immediately
        immediateAuthCheck();
    } else {
        // Document already loaded, still run check
        immediateAuthCheck();
    }

    // 2. Set up event listeners when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            injectNoCacheMetaTags();
            detectMobileSwipeBack();
            setupPeriodicChecks();
        });
    } else {
        injectNoCacheMetaTags();
        detectMobileSwipeBack();
        setupPeriodicChecks();
    }

    // 3. Listen for pageshow (critical for bfcache handling)
    window.addEventListener('pageshow', handlePageShow);

    // 4. Listen for visibility changes
    document.addEventListener('visibilitychange', handleVisibilityChange);

    // 5. Handle popstate (back/forward buttons)
    window.addEventListener('popstate', function(e) {
        // User pressed back or forward button
        validateAuthWithServer();
    });

    // 6. Beforeunload - clear auth flags when leaving
    window.addEventListener('beforeunload', function() {
        // Don't clear on normal navigation, only on actual logout
        // This is handled by the logout page
    });

})();
