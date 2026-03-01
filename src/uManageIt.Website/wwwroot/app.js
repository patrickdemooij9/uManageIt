window.uManageItApi = {
  async request(url, method, body) {
    const options = {
      method: method || 'GET',
      credentials: 'include',
      headers: {
        'Accept': 'application/json'
      }
    };

    if (body !== null && body !== undefined) {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(body);
    }

    const response = await fetch(url, options);
    const contentType = response.headers.get('content-type') || '';

    if (!response.ok) {
      let errorText = `${response.status} ${response.statusText}`;
      try {
        const errorData = contentType.includes('application/json')
          ? await response.json()
          : await response.text();
        errorText = typeof errorData === 'string' ? errorData : JSON.stringify(errorData);
      } catch {
        // Ignore parse errors.
      }

      throw new Error(errorText);
    }

    if (response.status === 204 || response.status === 202) {
      return null;
    }

    if (contentType.includes('application/json')) {
      return await response.json();
    }

    return await response.text();
  }
};
