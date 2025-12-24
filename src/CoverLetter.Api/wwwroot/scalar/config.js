export default {
  onBeforeRequest: ({ request }) => {
    request.headers.set("X-User-Id", "test")
  },
}
