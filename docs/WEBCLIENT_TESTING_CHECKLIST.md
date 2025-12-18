# Web Client Testing Checklist

## Pre-Testing Setup

- [ ] Ensure Broca.ActivityPub.Server is running
- [ ] Confirm `AdminApiToken` is configured in server's `appsettings.json`
- [ ] Have a test actor created with a username
- [ ] Note the Actor ID URL (e.g., `https://localhost:5000/users/testuser`)
- [ ] Have the API key ready (matching `AdminApiToken`)

## Build & Run

```bash
cd /mnt/sdb/src/Broca
dotnet build
dotnet run --project src/Broca.Web
```

- [ ] Project builds without errors
- [ ] Web app starts successfully
- [ ] Navigate to `https://localhost:5001` (or configured port)

## Authentication Tests

### Login
- [ ] Navigate to `/login`
- [ ] Enter valid Actor ID
- [ ] Enter valid API Key
- [ ] Click "Login"
- [ ] Verify redirect to home page
- [ ] Check browser console for errors
- [ ] Verify navigation menu shows authenticated links (Inbox, Outbox, etc.)
- [ ] Verify user info panel appears at bottom of nav menu
- [ ] Verify display name shows correctly

### Login Error Handling
- [ ] Try login with invalid API key → should show error
- [ ] Try login with malformed Actor ID → should show error
- [ ] Try login with non-existent actor → should show error

### Session Persistence
- [ ] Login successfully
- [ ] Refresh the page
- [ ] Verify still authenticated (menu shows user info)
- [ ] Open browser DevTools → Application → Local Storage
- [ ] Verify keys exist: `broca.apiKey`, `broca.actorId`, `broca.actorData`

### Logout
- [ ] Navigate to `/profile`
- [ ] Click "Logout" button
- [ ] Verify redirect to login page
- [ ] Verify navigation menu no longer shows auth-only links
- [ ] Verify local storage cleared

## Page Navigation Tests

### Home Page
**Unauthenticated:**
- [ ] Shows welcome message
- [ ] Shows feature highlights
- [ ] Shows "Get Started" button that links to login

**Authenticated:**
- [ ] Shows "Welcome back, {Name}"
- [ ] Shows recent inbox activity preview (if any activities exist)
- [ ] Shows "View Full Inbox" button

### Inbox Page
- [ ] Navigate to `/inbox`
- [ ] Verify page loads without errors
- [ ] If activities exist:
  - [ ] Activities render correctly
  - [ ] Pagination works (scroll to load more)
  - [ ] Activity details are visible
- [ ] If no activities:
  - [ ] Shows empty state with helpful message

### Outbox Page
- [ ] Navigate to `/outbox`
- [ ] Verify page loads without errors
- [ ] If activities exist:
  - [ ] Activities render correctly
  - [ ] Shows recipient information
  - [ ] Pagination works
- [ ] If no activities:
  - [ ] Shows empty state

### Collections Page
- [ ] Navigate to `/collections`
- [ ] Verify tabs render (Followers, Following)
- [ ] Click "Followers" tab:
  - [ ] If followers exist, verify they render as actor cards
  - [ ] If no followers, shows empty state
- [ ] Click "Following" tab:
  - [ ] If following exist, verify they render
  - [ ] If not following anyone, shows empty state
- [ ] Verify actor profiles load in cards

### Profile Page
- [ ] Navigate to `/profile`
- [ ] Verify actor profile component renders
- [ ] Verify account information table shows:
  - [ ] Actor ID
  - [ ] Name
  - [ ] Preferred Username
  - [ ] Type
  - [ ] Inbox URL
  - [ ] Outbox URL
  - [ ] Followers URL
  - [ ] Following URL
- [ ] Verify logout button is present

## Network Request Tests

### HTTP Signatures
- [ ] Open browser DevTools → Network tab
- [ ] Navigate to Inbox or Outbox
- [ ] Find GET requests to inbox/outbox URLs
- [ ] Check request headers for:
  - [ ] `Signature` header is present
  - [ ] Contains `keyId`, `headers`, `signature`
  - [ ] Contains `(request-target)` in headers list

### API Key Transmission
- [ ] In DevTools → Network tab
- [ ] Filter for the actor profile request during login
- [ ] Verify `Authorization: Bearer {apiKey}` header is sent
- [ ] Verify response includes `privateKeyPem` in JSON

### CORS Fallback (Optional - requires remote server)
- [ ] Try to fetch a remote ActivityPub resource
- [ ] If CORS error occurs:
  - [ ] Verify fallback request to `/api/proxy`
  - [ ] Verify `url` query parameter contains encoded target URL
  - [ ] Verify content is successfully retrieved

## Component Tests

### ActivityFeed Component
- [ ] Verify it displays in Inbox, Outbox, and Home
- [ ] Verify loading state shows spinner
- [ ] Verify error state shows error message with retry button
- [ ] Verify empty state shows appropriate message
- [ ] Verify pagination/virtualization works

### ActorProfile Component
- [ ] Verify it renders in Collections page
- [ ] Verify it renders in Profile page
- [ ] Verify actor name, avatar, and info display

### CollectionLoader Component
- [ ] Verify it loads followers correctly
- [ ] Verify it loads following correctly
- [ ] Verify pagination works

## UI/UX Tests

### Responsive Design
- [ ] Test on desktop (full width)
- [ ] Test on tablet (medium width)
- [ ] Test on mobile (narrow width)
- [ ] Verify navigation menu adapts
- [ ] Verify cards and layouts stack properly

### Loading States
- [ ] Verify login button shows "Authenticating..." when loading
- [ ] Verify activity feeds show loading spinner
- [ ] Verify collection loaders show loading state

### Error States
- [ ] Verify error messages are clear and helpful
- [ ] Verify error states don't crash the page
- [ ] Verify retry buttons work

### Icons and Styling
- [ ] Verify all icons render (Fluent UI icons)
- [ ] Verify color scheme is consistent
- [ ] Verify spacing and padding look good
- [ ] Verify buttons have hover states

## Browser Compatibility

- [ ] Test in Chrome/Edge
- [ ] Test in Firefox
- [ ] Test in Safari (if available)
- [ ] Verify local storage works in all browsers
- [ ] Verify Fluent UI renders correctly

## Performance Tests

- [ ] Measure initial page load time
- [ ] Test with 100+ activities in inbox (if possible)
- [ ] Verify virtualization improves performance
- [ ] Check for memory leaks (DevTools → Performance → Memory)
- [ ] Verify no console errors or warnings

## Security Tests

### Storage Security
- [ ] Verify private key is stored in local storage (development only)
- [ ] Verify API key is stored securely
- [ ] Verify logout clears all sensitive data

### Request Security
- [ ] Verify all authenticated requests include signatures
- [ ] Verify signatures use correct algorithm (RSA-SHA256)
- [ ] Verify Date header is recent (within 5 minutes)

## Edge Cases

- [ ] Login with very long actor ID
- [ ] Login with special characters in username
- [ ] Navigate to protected page while not logged in (should redirect or show login prompt)
- [ ] Logout while on a protected page
- [ ] Refresh page while loading activities
- [ ] Network offline during request

## Bugs to Watch For

Common issues to check:
- [ ] "Cannot read property of undefined" errors
- [ ] CORS errors that don't trigger fallback
- [ ] Infinite loading states
- [ ] Activities not rendering
- [ ] Navigation menu not updating on auth change
- [ ] Local storage quota exceeded (very long sessions)

## Final Checks

- [ ] No errors in browser console
- [ ] No errors in server logs
- [ ] All pages are accessible via navigation
- [ ] All links work correctly
- [ ] Back button works as expected
- [ ] Direct URL navigation works (e.g., typing `/inbox` directly)

## Notes Section

Use this space to record any issues found during testing:

```
Issue: [Description]
Steps to reproduce:
1. 
2. 
3. 

Expected: [What should happen]
Actual: [What actually happened]
```

---

## Tested By

- Name: ___________________
- Date: ___________________
- Environment: ___________________
- Notes: ___________________
